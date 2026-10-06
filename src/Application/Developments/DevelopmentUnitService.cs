using Microsoft.EntityFrameworkCore;
using PIPDC.Application.Data;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Developments;

public class DevelopmentUnitService(
    IAppDbContext dbContext,
    IDevelopmentListingPromoter listingPromoter) : IDevelopmentUnitService
{
    public async Task<Result<IReadOnlyList<DevelopmentUnitDto>>> GetByProjectAsync(int projectId, CancellationToken ct)
    {
        if (!await dbContext.DevelopmentProjects.AnyAsync(p => p.Id == projectId, ct))
            return Result<IReadOnlyList<DevelopmentUnitDto>>.Failure(
                Error.NotFound("development.notfound", $"Development project with id {projectId} was not found."));

        var units = await dbContext.DevelopmentUnits
            .Where(u => u.DevelopmentProjectId == projectId)
            .OrderBy(u => u.UnitIdentifier)
            .ToListAsync(ct);

        var dtos = units.Select(ToDto).ToList();

        return Result<IReadOnlyList<DevelopmentUnitDto>>.Success(dtos);
    }

    public async Task<Result<DevelopmentUnitDto>> CreateAsync(int projectId, CreateDevelopmentUnitRequest request, CancellationToken ct)
    {
        if (!await dbContext.DevelopmentProjects.AnyAsync(p => p.Id == projectId, ct))
            return Result<DevelopmentUnitDto>.Failure(
                Error.NotFound("development.notfound", $"Development project with id {projectId} was not found."));

        if (await dbContext.DevelopmentUnits.AnyAsync(u => u.DevelopmentProjectId == projectId && u.UnitIdentifier == request.UnitIdentifier, ct))
            return Result<DevelopmentUnitDto>.Failure(
                Error.Conflict("unit.conflict", $"A unit with identifier '{request.UnitIdentifier}' already exists in this project."));

        var unit = new DevelopmentUnit
        {
            DevelopmentProjectId = projectId,
            UnitIdentifier = request.UnitIdentifier,
            UnitType = request.UnitType,
            Status = string.IsNullOrWhiteSpace(request.Status)
                ? DevelopmentUnitStatus.Available
                : Enum.Parse<DevelopmentUnitStatus>(request.Status, true),
            Price = request.Price,
            Currency = request.Currency ?? "NGN",
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        ApplyListingFields(unit, request.ListingType, request.PropertyType, request.Period,
            request.Bedrooms, request.Bathrooms, request.Size, request.SizeUnit,
            request.YearBuilt, request.Amenities);

        dbContext.DevelopmentUnits.Add(unit);
        await dbContext.SaveChangesAsync(ct);

        return Result<DevelopmentUnitDto>.Success(ToDto(unit));
    }

    public async Task<Result<DevelopmentUnitDto>> UpdateAsync(int projectId, int unitId, UpdateDevelopmentUnitRequest request, CancellationToken ct)
    {
        var unit = await dbContext.DevelopmentUnits
            .FirstOrDefaultAsync(u => u.Id == unitId && u.DevelopmentProjectId == projectId, ct);

        if (unit is null)
            return Result<DevelopmentUnitDto>.Failure(
                Error.NotFound("unit.notfound", $"Development unit with id {unitId} was not found in project {projectId}."));

        if (await dbContext.DevelopmentUnits.AnyAsync(u => u.DevelopmentProjectId == projectId && u.UnitIdentifier == request.UnitIdentifier && u.Id != unitId, ct))
            return Result<DevelopmentUnitDto>.Failure(
                Error.Conflict("unit.conflict", $"A unit with identifier '{request.UnitIdentifier}' already exists in this project."));

        unit.UnitIdentifier = request.UnitIdentifier;
        unit.UnitType = request.UnitType;
        unit.Status = Enum.Parse<DevelopmentUnitStatus>(request.Status, true);
        unit.Price = request.Price;
        unit.Currency = request.Currency ?? "NGN";
        unit.Description = request.Description;
        unit.UpdatedAt = DateTime.UtcNow;

        ApplyListingFields(unit, request.ListingType, request.PropertyType, request.Period,
            request.Bedrooms, request.Bathrooms, request.Size, request.SizeUnit,
            request.YearBuilt, request.Amenities);

        await dbContext.SaveChangesAsync(ct);

        // The unit may have just become listable, or been given the detail a
        // pending promotion needed, so retry promotion now rather than waiting for
        // the project to be saved again.
        if (unit.PropertyId is null)
        {
            var project = await dbContext.DevelopmentProjects
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == projectId, ct);
            if (project is not null && project.Status == DevelopmentProjectStatus.Completed)
            {
                var promotion = await listingPromoter.PromoteUnitAsync(project, unit, ct);
                if (promotion.IsFailure)
                    return Result<DevelopmentUnitDto>.Failure(promotion.Error);
                if (promotion.Value > 0)
                    await dbContext.SaveChangesAsync(ct);
            }
        }

        return Result<DevelopmentUnitDto>.Success(ToDto(unit));
    }

    /// <summary>
    /// Turns a ready unit into its own listing on request.
    ///
    /// Promotion otherwise only happens as a side effect of saving the unit or the
    /// project, which leaves the Units tab showing a "Ready to list" unit with no
    /// control anywhere to act on it - even though the completion notification tells
    /// the administrator to publish each unit from that table. This is the explicit
    /// action that sentence refers to. It is idempotent: a unit that already has a
    /// listing returns its current state rather than creating a second one, which
    /// the unique index on PropertyId would reject anyway.
    /// </summary>
    public async Task<Result<DevelopmentUnitDto>> PromoteAsync(int projectId, int unitId, CancellationToken ct)
    {
        var unit = await dbContext.DevelopmentUnits
            .FirstOrDefaultAsync(u => u.Id == unitId && u.DevelopmentProjectId == projectId, ct);

        if (unit is null)
            return Result<DevelopmentUnitDto>.Failure(
                Error.NotFound("unit.notfound", $"Development unit with id {unitId} was not found in project {projectId}."));

        if (unit.PropertyId is not null)
            return await ReloadDtoAsync(unitId, ct);

        var project = await dbContext.DevelopmentProjects
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project is null)
            return Result<DevelopmentUnitDto>.Failure(
                Error.NotFound("development.notfound", $"Development project with id {projectId} was not found."));

        // The same gate UpdateAsync applies. Listing before the estate is finished
        // would publish addresses for houses that do not exist yet.
        if (project.Status != DevelopmentProjectStatus.Completed)
            return Result<DevelopmentUnitDto>.Failure(Error.Validation(
                "unit.projectincomplete",
                $"\"{project.Name}\" must be Completed before its units can be listed."));

        if (!DevelopmentListingPromoter.IsListable(unit))
            return Result<DevelopmentUnitDto>.Failure(Error.Validation(
                "unit.notlistable",
                $"Add {string.Join(", ", DevelopmentListingPromoter.MissingListingDetails(unit).Select(d => d.ToLower()))} to \"{unit.UnitIdentifier}\" before listing it."));

        var promotion = await listingPromoter.PromoteUnitAsync(project, unit, ct);
        if (promotion.IsFailure)
            return Result<DevelopmentUnitDto>.Failure(promotion.Error);

        // PromoteUnitAsync assigns unit.PropertyId after its own save, so the link
        // is persisted here rather than being left for the next unrelated update.
        await dbContext.SaveChangesAsync(ct);

        return await ReloadDtoAsync(unitId, ct);
    }

    private async Task<Result<DevelopmentUnitDto>> ReloadDtoAsync(int unitId, CancellationToken ct)
    {
        var refreshed = await dbContext.DevelopmentUnits
            .Include(u => u.Property)
            .FirstOrDefaultAsync(u => u.Id == unitId, ct);

        return refreshed is null
            ? Result<DevelopmentUnitDto>.Failure(
                Error.NotFound("unit.notfound", $"Development unit with id {unitId} was not found."))
            : Result<DevelopmentUnitDto>.Success(ToDto(refreshed));
    }

    public async Task<Result> DeleteAsync(int projectId, int unitId, CancellationToken ct)
    {
        var unit = await dbContext.DevelopmentUnits
            .FirstOrDefaultAsync(u => u.Id == unitId && u.DevelopmentProjectId == projectId, ct);

        if (unit is null)
            return Result.Failure(
                Error.NotFound("unit.notfound", $"Development unit with id {unitId} was not found in project {projectId}."));

        // The listing survives: deleting a unit from the estate is not the same as
        // withdrawing a house that is already on sale. The unique index on PropertyId
        // means the freed slot cannot silently produce a second listing.
        dbContext.DevelopmentUnits.Remove(unit);
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// Copies the listing descriptor off the request. A unit is the individual
    /// house, so these are the fields that end up on the public listing.
    /// </summary>
    private static void ApplyListingFields(
        DevelopmentUnit unit,
        string? listingType,
        string? propertyType,
        string? period,
        int? bedrooms,
        int? bathrooms,
        double? size,
        string? sizeUnit,
        int? yearBuilt,
        IReadOnlyList<string>? amenities)
    {
        // Unknown values are ignored rather than throwing, so a stale client cannot
        // make the whole unit form unusable.
        if (Enum.TryParse<ListingType>(listingType, true, out var parsedListingType))
            unit.ListingType = parsedListingType;
        if (Enum.TryParse<PropertyType>(propertyType, true, out var parsedPropertyType))
            unit.PropertyType = parsedPropertyType;

        unit.Period = period;
        unit.Bedrooms = bedrooms;
        unit.Bathrooms = bathrooms;
        unit.Size = size;
        unit.SizeUnit = string.IsNullOrWhiteSpace(sizeUnit) ? "sqm" : sizeUnit;
        unit.YearBuilt = yearBuilt;
        unit.Amenities = amenities?.ToList() ?? [];
    }

    private static DevelopmentUnitDto ToDto(DevelopmentUnit u) => new(
        u.Id, u.UnitIdentifier, u.UnitType, u.Status.ToString(),
        u.Price, u.Currency, u.Description, u.CreatedAt, u.UpdatedAt,
        u.ListingType.ToString(), u.PropertyType.ToString(), u.Period,
        u.Bedrooms, u.Bathrooms, u.Size, u.SizeUnit, u.YearBuilt,
        u.Amenities, u.PropertyId, u.Property?.Status.ToString(),
        DevelopmentListingPromoter.IsListable(u),
        DevelopmentListingPromoter.MissingListingDetails(u));
}
