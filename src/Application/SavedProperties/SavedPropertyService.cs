using Microsoft.EntityFrameworkCore;
using PIPDC.Application.Common;
using PIPDC.Application.Data;
using PIPDC.Application.Properties;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;

namespace PIPDC.Application.SavedProperties;

public class SavedPropertyService(IAppDbContext dbContext) : ISavedPropertyService
{
    /// <summary>
    /// The ids a user is allowed to see. A saved bookmark is a client-side
    /// shortcut to a listing, so it must not outlive the listing's visibility:
    /// without this, a suspended agent's inventory would keep flowing to every
    /// client through the saved list, the saved-id list and the saved counter.
    /// </summary>
    private IQueryable<int> VisibleSavedPropertyIds(string userId, bool includeSuspendedAgents)
    {
        IQueryable<Property> properties = dbContext.Properties;
        if (!includeSuspendedAgents)
            properties = properties.VisibleProperties();

        var visibleIds = properties.Select(p => p.Id);

        return dbContext.SavedProperties
            .Where(s => s.UserId == userId && visibleIds.Contains(s.PropertyId))
            .Select(s => s.PropertyId);
    }

    public async Task<Result<PaginatedResult<SavedPropertyDto>>> GetSavedAsync(
        string userId,
        bool includeSuspendedAgents,
        SavedPropertyQueryParameters q,
        CancellationToken ct)
    {
        var visibleIds = VisibleSavedPropertyIds(userId, includeSuspendedAgents);

        // Counted from the visible set so the page count matches the rows the
        // caller actually receives.
        var totalCount = await visibleIds.CountAsync(ct);

        // Paging must be applied to the visible set, not to the caller's full
        // saved list. Filtering after Skip/Take meant a page could come back
        // short (or empty) whenever the hidden rows happened to fall inside the
        // requested window, while totalCount still counted them.
        var savedRows = await dbContext.SavedProperties
            .Where(s => s.UserId == userId)
            .Join(visibleIds, s => s.PropertyId, v => v, (s, _) => s)
            .OrderByDescending(s => s.CreatedAt)
            .Skip((q.PageNumber - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(s => new { s.PropertyId, SavedAt = s.CreatedAt })
            .ToListAsync(ct);

        var propertyIds = savedRows.Select(s => s.PropertyId).ToList();
        var savedAtMap = savedRows.ToDictionary(s => s.PropertyId, s => s.SavedAt);

        IQueryable<Property> query = dbContext.Properties
            .Include(p => p.Agent)
                .ThenInclude(a => a!.User)
            .Include(p => p.PropertyImages)
            .Where(p => propertyIds.Contains(p.Id));

        if (!includeSuspendedAgents)
            query = query.VisibleProperties();

        var properties = await query.ToListAsync(ct);

        var propertyMap = properties.ToDictionary(p => p.Id);

        var counts = await dbContext.Enquiries
            .Where(e => propertyIds.Contains(e.PropertyId))
            .GroupBy(e => e.PropertyId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var dtos = savedRows
            .Where(s => propertyMap.ContainsKey(s.PropertyId))
            .Select(s => new SavedPropertyDto(
                propertyMap[s.PropertyId].ToDto(isSaved: true, enquiryCount: counts.GetValueOrDefault(s.PropertyId)),
                savedAtMap[s.PropertyId]))
            .ToList();

        return Result<PaginatedResult<SavedPropertyDto>>.Success(
            PaginatedResult<SavedPropertyDto>.Create(dtos, totalCount, q.PageNumber, q.PageSize));
    }

    public async Task<Result<IReadOnlyList<int>>> GetSavedIdsAsync(
        string userId,
        bool includeSuspendedAgents,
        CancellationToken ct)
    {
        var ids = await VisibleSavedPropertyIds(userId, includeSuspendedAgents)
            .OrderByDescending(id => id)
            .ToListAsync(ct);

        return Result<IReadOnlyList<int>>.Success(ids);
    }

    public async Task<Result> SaveAsync(
        string userId,
        int propertyId,
        bool includeSuspendedAgents,
        CancellationToken ct)
    {
        // Visibility-aware existence check. An unfiltered check would let a
        // client confirm a hidden listing id by observing the difference between
        // 200 and 404.
        IQueryable<Property> query = dbContext.Properties.Where(p => p.Id == propertyId);
        if (!includeSuspendedAgents)
            query = query.VisibleProperties();

        if (!await query.AnyAsync(ct))
            return Result.Failure(
                Error.NotFound("property.notfound", $"Property with id {propertyId} was not found."));

        if (await dbContext.SavedProperties.AnyAsync(s => s.UserId == userId && s.PropertyId == propertyId, ct))
            return Result.Success();

        dbContext.SavedProperties.Add(new SavedProperty
        {
            UserId = userId,
            PropertyId = propertyId,
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UnsaveAsync(string userId, int propertyId, CancellationToken ct)
    {
        var saved = await dbContext.SavedProperties
            .FirstOrDefaultAsync(s => s.UserId == userId && s.PropertyId == propertyId, ct);

        if (saved is null)
            return Result.Success();

        dbContext.SavedProperties.Remove(saved);
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }
}
