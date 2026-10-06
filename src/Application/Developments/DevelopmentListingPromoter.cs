using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using PIPDC.Application.Auth;
using PIPDC.Application.Data;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Developments;

/// <summary>
/// Promotes the units of a finished development into real listings.
///
/// The distinction matters: a development project is the estate, and its units are
/// the individual houses clients buy. Only a unit becomes a <see cref="Property"/>.
/// Listing the project itself produced a property with no price, no bedrooms and no
/// amenities, which is not something a client can buy or an agent can sell.
/// </summary>
public interface IDevelopmentListingPromoter
{
    /// <summary>Promotes every still-unlisted unit of the project that is ready.</summary>
    Task<Result<int>> PromoteProjectUnitsAsync(DevelopmentProject project, CancellationToken ct);

    /// <summary>Promotes a single unit if it is ready and not already listed.</summary>
    Task<Result<int>> PromoteUnitAsync(DevelopmentProject project, DevelopmentUnit unit, CancellationToken ct);
}

public class DevelopmentListingPromoter(
    IAppDbContext dbContext,
    UserManager<AppUser> userManager,
    ILogger<DevelopmentListingPromoter> logger) : IDevelopmentListingPromoter
{
    public async Task<Result<int>> PromoteProjectUnitsAsync(DevelopmentProject project, CancellationToken ct)
    {
        var units = await dbContext.DevelopmentUnits
            .Where(u => u.DevelopmentProjectId == project.Id)
            .ToListAsync(ct);

        var promoted = 0;
        foreach (var unit in units)
        {
            var result = await PromoteUnitAsync(project, unit, ct);
            if (result.IsFailure)
                return Result<int>.Failure(result.Error);
            promoted += result.Value;
        }

        if (promoted > 0)
            await dbContext.SaveChangesAsync(ct);

        return Result<int>.Success(promoted);
    }

    public async Task<Result<int>> PromoteUnitAsync(DevelopmentProject project, DevelopmentUnit unit, CancellationToken ct)
    {
        // Already has a listing, so this is a re-completion rather than new work.
        // The unique index on PropertyId is what actually guarantees one listing per
        // unit; this check keeps the common path from doing pointless work.
        if (unit.PropertyId is not null)
            return Result<int>.Success(0);

        if (!IsListable(unit))
        {
            logger.LogInformation(
                "Unit {UnitId} ({Identifier}) on project {ProjectId} is not listable yet and was not promoted.",
                unit.Id, unit.UnitIdentifier, project.Id);
            return Result<int>.Success(0);
        }

        var listing = await BuildListingFromUnitAsync(project, unit, ct);
        dbContext.Properties.Add(listing);
        await dbContext.SaveChangesAsync(ct);

        // The unit borrows the estate's photography, because a unit does not carry
        // its own. Added after the save so the listing has an id to attach to.
        var cover = project.Images
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.Id)
            .FirstOrDefault();
        if (cover is not null)
        {
            dbContext.PropertyImages.Add(new PropertyImage
            {
                PropertyId = listing.Id,
                Url = cover.Url,
                PublicId = cover.PublicId,
                IsCover = true,
                DisplayOrder = 0,
                CreatedAt = DateTime.UtcNow
            });
        }

        unit.PropertyId = listing.Id;
        return Result<int>.Success(1);
    }

    /// <summary>
    /// The minimum a unit needs before it can become a listing. Anything less and the
    /// public page renders a row of empty dashes.
    /// </summary>
    public static bool IsListable(DevelopmentUnit unit) =>
        unit.Price is > 0
        && !string.IsNullOrWhiteSpace(unit.UnitIdentifier)
        && !string.IsNullOrWhiteSpace(unit.UnitType);

    /// <summary>
    /// Names the fields still missing, so the Units tab can say what to fill in
    /// instead of showing an unexplained "not listable" badge.
    /// </summary>
    public static string[] MissingListingDetails(DevelopmentUnit unit)
    {
        var missing = new List<string>();
        if (unit.Price is not > 0)
            missing.Add("Price");
        if (string.IsNullOrWhiteSpace(unit.UnitIdentifier))
            missing.Add("Unit identifier");
        if (string.IsNullOrWhiteSpace(unit.UnitType))
            missing.Add("Unit type");
        return [.. missing];
    }

    /// <summary>
    /// Builds the listing for one unit, taking location from the parent project
    /// because a unit does not repeat it.
    ///
    /// The listing starts <see cref="PropertyStatus.Unavailable"/>: it is complete
    /// enough to review, but publishing stays an explicit admin action.
    /// </summary>
    private async Task<Property> BuildListingFromUnitAsync(
        DevelopmentProject project,
        DevelopmentUnit unit,
        CancellationToken ct)
    {
        var title = $"{unit.UnitIdentifier} - {unit.UnitType}";
        var slug = await EnsureUniquePropertySlugAsync(
            $"{project.Name}-{unit.UnitIdentifier}",
            ct);

        var (state, city) = await ResolveLocationAsync(project, ct);

        // A unit description is short, so fall back to the project's own copy
        // rather than publishing an empty "About this property" section.
        var description = string.IsNullOrWhiteSpace(unit.Description)
            ? project.Description
            : unit.Description;

        return new Property
        {
            Title = title,
            Description = description,
            Slug = slug,
            Price = unit.Price!.Value,
            Currency = unit.Currency,
            Period = unit.Period,
            Status = PropertyStatus.Unavailable,
            PropertyType = unit.PropertyType,
            ListingType = unit.ListingType,
            Bedrooms = unit.Bedrooms,
            Bathrooms = unit.Bathrooms,
            Size = unit.Size,
            SizeUnit = unit.SizeUnit,
            YearBuilt = unit.YearBuilt,
            Amenities = [.. unit.Amenities],
            LocationId = project.LocationRefId,
            Address = project.Location,
            State = state,
            City = city,
            Featured = false,
            AgentId = null,
            CreatedByUserId = await ResolveActorIdAsync(ct),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// A unit has no location of its own, so it inherits the project's. The
    /// reference may point at a state or at a city/LGA.
    /// </summary>
    private async Task<(string State, string City)> ResolveLocationAsync(
        DevelopmentProject project,
        CancellationToken ct)
    {
        var state = string.Empty;
        var city = string.Empty;

        if (project.LocationRefId is not null)
        {
            var location = await dbContext.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == project.LocationRefId.Value, ct);

            if (location is not null)
            {
                if (location.Type == LocationType.State)
                {
                    state = location.Name;
                }
                else
                {
                    city = location.Name;
                    if (location.ParentId is not null)
                    {
                        var parent = await dbContext.Locations
                            .AsNoTracking()
                            .FirstOrDefaultAsync(l => l.Id == location.ParentId.Value, ct);
                        state = parent?.Name ?? string.Empty;
                    }
                }
            }
        }

        // The free-text location is the fallback, and it is the only place the
        // missing half of the pair can come from.
        if (string.IsNullOrWhiteSpace(city))
            city = project.Location;
        if (string.IsNullOrWhiteSpace(state))
            state = project.Location;

        return (state, city);
    }

    private async Task<string> EnsureUniquePropertySlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Slugify(name);

        var slug = baseSlug;
        var suffix = 2;
        while (await dbContext.Properties.AnyAsync(p => p.Slug == slug, ct))
        {
            slug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return slug;
    }

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.Trim().ToLower(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length == 0 ? "property" : slug;
    }

    private async Task<string> ResolveActorIdAsync(CancellationToken ct)
    {
        var admin = await userManager.GetUsersInRoleAsync(Roles.Admin);
        return admin.FirstOrDefault()?.Id
            ?? (await dbContext.Users.Select(u => u.Id).FirstOrDefaultAsync(ct))
            ?? "system";
    }
}