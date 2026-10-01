using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PIPDC.Application.Common;
using PIPDC.Application.Data;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Locations;

public class LocationService(IAppDbContext dbContext) : ILocationService
{
    public async Task<Result<IReadOnlyList<LocationDto>>> GetAllAsync(string? type, int? parentId, CancellationToken ct)
    {
        IQueryable<Location> query = dbContext.Locations;

        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<LocationType>(type, true, out var parsedType))
            query = query.Where(l => l.Type == parsedType);

        if (parentId.HasValue)
            query = query.Where(l => l.ParentId == parentId);

        var items = await query
            .OrderBy(l => l.Name)
            .Select(l => new LocationDto(
                l.Id,
                l.Name,
                l.Slug,
                l.Type.ToString(),
                l.ParentId,
                l.Parent != null ? l.Parent.Name : null,
                l.Children.Count))
            .ToListAsync(ct);

        return Result<IReadOnlyList<LocationDto>>.Success(items);
    }

    public async Task<Result<LocationDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var l = await dbContext.Locations
            .Include(x => x.Parent)
            .Include(x => x.Children)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (l is null)
            return Result<LocationDto>.Failure(
                Error.NotFound("location.notfound", $"Location with id {id} was not found."));

        return Result<LocationDto>.Success(new LocationDto(
            l.Id,
            l.Name,
            l.Slug,
            l.Type.ToString(),
            l.ParentId,
            l.Parent?.Name,
            l.Children.Count));
    }

    public async Task<Result<IReadOnlyList<LocationDto>>> GetHierarchyAsync(int? stateId, CancellationToken ct)
    {
        IQueryable<Location> query = dbContext.Locations
            .Include(l => l.Children)
                .ThenInclude(c => c.Children)
                    .ThenInclude(a => a.Children);

        if (stateId.HasValue)
            query = query.Where(l => l.Id == stateId && l.Type == LocationType.State);

        var states = await query
            .Where(l => l.Type == LocationType.State)
            .OrderBy(l => l.Name)
            .ToListAsync(ct);

        var result = states.Select(s => new LocationDto(
            s.Id,
            s.Name,
            s.Slug,
            s.Type.ToString(),
            s.ParentId,
            null,
            s.Children.Count)).ToList();

        return Result<IReadOnlyList<LocationDto>>.Success(result);
    }

    public async Task<Result<LocationDto>> CreateAsync(CreateLocationRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<LocationType>(request.Type, true, out var locationType))
            return Result<LocationDto>.Failure(
                Error.Validation("location.invalidtype", $"'{request.Type}' is not a valid location type. Use: State, LGA, City, or Area."));

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<LocationDto>.Failure(
                Error.Validation("location.name", "Location name is required."));

        var name = request.Name.Trim();

        var parentCheck = await ValidateParentAsync(locationType, request.ParentId, ct);
        if (parentCheck is not null)
            return Result<LocationDto>.Failure(parentCheck);

        var slug = Slugify(request.Slug ?? name);

        // Uniqueness is per parent. Matches UNIQUE (Name, ParentId) for children
        // and UNIQUE (Name) WHERE "ParentId" IS NULL for roots.
        if (await dbContext.Locations.AnyAsync(l => l.Name == name && l.ParentId == request.ParentId, ct))
            return Result<LocationDto>.Failure(
                Error.Validation("location.duplicate", $"A location named '{name}' already exists at this level."));

        if (await dbContext.Locations.AnyAsync(l => l.Slug == slug, ct))
            slug = await EnsureUniqueSlugAsync(slug, ct);

        var location = new Location
        {
            Name = name,
            Slug = slug,
            Type = locationType,
            ParentId = request.ParentId,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Locations.Add(location);
        await dbContext.SaveChangesAsync(ct);

        return Result<LocationDto>.Success(new LocationDto(
            location.Id,
            location.Name,
            location.Slug,
            location.Type.ToString(),
            location.ParentId,
            null,
            0));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        var location = await dbContext.Locations.FindAsync([id], ct);
        if (location is null)
            return Result.Failure(Error.NotFound("location.notfound", $"Location with id {id} was not found."));

        var hasChildren = await dbContext.Locations.AnyAsync(l => l.ParentId == id, ct);
        if (hasChildren)
            return Result.Failure(Error.Validation("location.haschildren", "Cannot delete a location that has child locations. Delete the children first."));

        var hasProperties = await dbContext.Properties.AnyAsync(p => p.LocationId == id, ct);
        if (hasProperties)
            return Result.Failure(Error.Validation("location.hasreferences", "Cannot delete a location that is assigned to properties. Remove the assignments first."));

        dbContext.Locations.Remove(location);
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// Enforces the intended hierarchy: State (root) &gt; LGA &gt; City &gt; Area.
    ///
    /// City is intentionally allowed to hang directly off a State, because the
    /// current property form collects a city name inline without an LGA step.
    /// Accepting that shape now keeps existing data and the existing UI working
    /// while still refusing structurally impossible trees.
    /// </summary>
    private async Task<Error?> ValidateParentAsync(LocationType type, int? parentId, CancellationToken ct)
    {
        if (type == LocationType.State)
        {
            if (parentId.HasValue)
                return Error.Validation("location.rootparent", "A State is a root location and cannot have a parent.");

            return null;
        }

        if (!parentId.HasValue)
            return Error.Validation("location.parentrequired", $"A {type} must belong to a parent location.");

        var parent = await dbContext.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == parentId.Value, ct);

        if (parent is null)
            return Error.NotFound("location.parentnotfound", $"Parent location with id {parentId.Value} was not found.");

        var allowed = type switch
        {
            LocationType.LGA => parent.Type == LocationType.State,
            LocationType.City => parent.Type is LocationType.LGA or LocationType.State,
            LocationType.Area => parent.Type is LocationType.City or LocationType.LGA,
            _ => false,
        };

        if (!allowed)
            return Error.Validation(
                "location.invalidhierarchy",
                $"A {type} cannot be placed under a {parent.Type}.");

        return null;
    }

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.Trim().ToLower(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length == 0 ? "location" : slug;
    }

    private async Task<string> EnsureUniqueSlugAsync(string baseSlug, CancellationToken ct)
    {
        var candidate = baseSlug;
        var suffix = 2;
        while (await dbContext.Locations.AnyAsync(l => l.Slug == candidate, ct))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }
        return candidate;
    }
}
