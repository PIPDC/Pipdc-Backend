using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PIPDC.Application.Auth;
using PIPDC.Application.Common;
using PIPDC.Application.Data;
using PIPDC.Application.Services;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Properties;

public class PropertyService(IAppDbContext dbContext, IImageService imageService) : IPropertyService
{
    // Batch 5 "near you" tuning. The project has no existing options pattern for
    // read limits, so these stay as plain constants rather than introducing
    // configuration infrastructure for two numbers. The result count matches the
    // six-card grid the home page already uses for featured properties.
    private const int NearbyResultLimit = 6;
    private const double NearbyMaxRadiusKm = 50.0;
    private const double KmPerDegreeLatitude = 111.32;

    private const string NearbyModeDistance = "distance";
    private const string NearbyModeArea = "area";
    private const string NearbyModeNone = "none";

    public async Task<Result<PaginatedResult<PropertyDto>>> GetAllAsync(
        PropertyQueryParameters q,
        string? currentUserId,
        bool includeSuspendedAgents,
        CancellationToken ct)
    {
        // Properties belonging to a suspended agent are hidden from public
        // listings. includeSuspendedAgents is supplied by the API layer from the
        // caller's role, not from a query parameter. Properties with no agent are
        // unaffected: they are not moderated by an agent decision.
        IQueryable<Property> query = includeSuspendedAgents
            ? dbContext.Properties
            : dbContext.Properties.VisibleProperties();

        var keyword = q.Query ?? q.Keyword;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var search = keyword.ToLower();
            query = query.Where(p => p.Title.ToLower().Contains(search)
                                  || p.Description.ToLower().Contains(search)
                                  || p.City.ToLower().Contains(search)
                                  || p.Area!.ToLower().Contains(search));
        }

        if (q.LocationId.HasValue)
        {
            query = query.Where(p => p.LocationId == q.LocationId);
        }
        else if (!string.IsNullOrWhiteSpace(q.Location))
        {
            var location = NormalizeLocation(q.Location);
            if (string.IsNullOrEmpty(location))
                location = q.Location.ToLower().Trim();
            query = query.Where(p => p.Area!.ToLower().Contains(location)
                                  || p.City.ToLower().Contains(location)
                                  || p.State.ToLower().Contains(location));
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(q.City))
            {
                var city = q.City.ToLower();
                query = query.Where(p => p.City.ToLower().Contains(city));
            }

            if (!string.IsNullOrWhiteSpace(q.State))
            {
                var state = q.State.ToLower();
                query = query.Where(p => p.State.ToLower().Contains(state));
            }
        }

        if (PropertyTypeDisplay.TryParse(q.Type ?? q.PropertyType, out var propertyType))
            query = query.Where(p => p.PropertyType == propertyType);

        if (Enum.TryParse<ListingType>(q.ListingType, true, out var listingType))
            query = query.Where(p => p.ListingType == listingType);

        query = ApplyStatusFilter(query, q.Status);

        if (q.MinPrice.HasValue)
            query = query.Where(p => p.Price >= q.MinPrice.Value);

        if (q.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= q.MaxPrice.Value);

        if (q.Bedrooms.HasValue)
            query = query.Where(p => p.Bedrooms >= q.Bedrooms.Value);

        if (q.Bathrooms.HasValue)
            query = query.Where(p => p.Bathrooms >= q.Bathrooms.Value);

        if (q.AgentId.HasValue)
            query = query.Where(p => p.AgentId == q.AgentId.Value);

        var totalCount = await query.CountAsync(ct);

        query = ApplySorting(query, q);

        var items = await Project(query, currentUserId)
            .Skip((q.EffectivePageNumber - 1) * q.EffectivePageSize)
            .Take(q.EffectivePageSize)
            .ToListAsync(ct);

        var dtos = items.Select(ToDto).ToList();

        return Result<PaginatedResult<PropertyDto>>.Success(
            PaginatedResult<PropertyDto>.Create(dtos, totalCount, q.EffectivePageNumber, q.EffectivePageSize));
    }

    public async Task<Result<PropertyDto>> GetByIdAsync(
        int id, string? currentUserId, bool includeSuspendedAgents, CancellationToken ct)
    {
        var property = await LoadPropertyAsync(id, includeSuspendedAgents, ct);
        if (property is null)
            return Result<PropertyDto>.Failure(
                Error.NotFound("property.notfound", $"Property with id {id} was not found."));

        return Result<PropertyDto>.Success(property.ToDto(
            await IsSavedAsync(id, currentUserId, ct), await EnquiryCountAsync(id, ct)));
    }

    public async Task<Result<PropertyDto>> GetBySlugAsync(
        string slug, string? currentUserId, bool includeSuspendedAgents, CancellationToken ct)
    {
        IQueryable<Property> query = dbContext.Properties
            .Include(p => p.Agent)
                .ThenInclude(a => a!.User)
            .Include(p => p.PropertyImages);

        if (!includeSuspendedAgents)
            query = query.VisibleProperties();

        var property = await query.FirstOrDefaultAsync(p => p.Slug == slug, ct);

        if (property is null)
            return Result<PropertyDto>.Failure(
                Error.NotFound("property.notfound", $"Property with slug '{slug}' was not found."));

        return Result<PropertyDto>.Success(property.ToDto(
            await IsSavedAsync(property.Id, currentUserId, ct), await EnquiryCountAsync(property.Id, ct)));
    }

    public async Task<Result<IReadOnlyList<PropertyDto>>> GetFeaturedAsync(
        string? currentUserId, bool includeSuspendedAgents, CancellationToken ct)
    {
        IQueryable<Property> query = dbContext.Properties.Where(p => p.Featured
            && (p.Status == PropertyStatus.Available || p.Status == PropertyStatus.Pending));

        if (!includeSuspendedAgents)
            query = query.VisibleProperties();

        var items = await Project(query.OrderByDescending(p => p.CreatedAt), currentUserId)
            .Take(6)
            .ToListAsync(ct);

        return Result<IReadOnlyList<PropertyDto>>.Success(items.Select(ToDto).ToList());
    }

    public async Task<Result<IReadOnlyList<PropertyDto>>> GetSimilarAsync(
        int id, string? currentUserId, bool includeSuspendedAgents, CancellationToken ct)
    {
        // The seed is read through the same visibility rule as the candidates.
        // Loading it unfiltered would answer 200 with an empty list for a hidden
        // listing where every other route answers 404, which confirms the id
        // exists and leaks that the agent has been suspended.
        IQueryable<Property> seedQuery = dbContext.Properties;
        if (!includeSuspendedAgents)
            seedQuery = seedQuery.VisibleProperties();

        var property = await seedQuery.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (property is null)
            return Result<IReadOnlyList<PropertyDto>>.Failure(
                Error.NotFound("property.notfound", $"Property with id {id} was not found."));

        IQueryable<Property> query = dbContext.Properties.Where(p => p.Id != id
            && p.PropertyType == property.PropertyType
            && (p.Status == PropertyStatus.Available || p.Status == PropertyStatus.Pending));

        if (!includeSuspendedAgents)
            query = query.VisibleProperties();

        var items = await Project(query.OrderByDescending(p => p.CreatedAt), currentUserId)
            .Take(3)
            .ToListAsync(ct);

        return Result<IReadOnlyList<PropertyDto>>.Success(items.Select(ToDto).ToList());
    }

    public async Task<Result<NearbyPropertiesDto>> GetNearbyAsync(string currentUserId, CancellationToken ct)
    {
        // Batch 5. The caller is always the signed-in user: the id is never read from
        // the request, so one account cannot ask what is near another.
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUserId, ct);

        if (user is null)
            return Result<NearbyPropertiesDto>.Failure(
                Error.NotFound("user.notfound", "User not found."));

        string? locationName = user.LocationId is null
            ? null
            : await dbContext.Locations
                .AsNoTracking()
                .Where(l => l.Id == user.LocationId.Value)
                .Select(l => l.Name)
                .FirstOrDefaultAsync(ct);

        var hasCoordinates = user.Latitude.HasValue && user.Longitude.HasValue;

        // Nothing to anchor on. The caller turns this into a prompt to set a
        // location; it must never fall back to "here are some properties anyway".
        if (!hasCoordinates && string.IsNullOrWhiteSpace(locationName))
            return Result<NearbyPropertiesDto>.Success(new NearbyPropertiesDto(
                [], false, false, locationName, NearbyModeNone));

        IQueryable<Property> query = dbContext.Properties
            .Where(p => p.Status == PropertyStatus.Available || p.Status == PropertyStatus.Pending)
            .VisibleProperties();

        if (!hasCoordinates)
            return await AreaMatchAsync(query, locationName!, currentUserId, ct);

        // Only rows that actually carry coordinates can be ranked by distance. A
        // property without them stays reachable through normal search and listing; it
        // is simply not claimed to be nearby.
        query = query.Where(p => p.Latitude != null && p.Longitude != null);

        var lat = user.Latitude!.Value;
        var lon = user.Longitude!.Value;

        // A bounding box is cheap for PostgreSQL to evaluate and cuts the candidate
        // set before any trigonometry happens. Exact Haversine then runs in memory
        // over only those candidates, so the whole table is never loaded and no
        // spatial extension is required.
        var latDelta = NearbyMaxRadiusKm / KmPerDegreeLatitude;
        var cosLat = Math.Cos(lat * Math.PI / 180.0);
        var lonDelta = Math.Abs(cosLat) < 0.000001
            ? 180.0
            : NearbyMaxRadiusKm / (KmPerDegreeLatitude * Math.Abs(cosLat));

        var candidates = await query
            .AsNoTracking()
            .Where(p => p.Latitude >= lat - latDelta && p.Latitude <= lat + latDelta
                && p.Longitude >= lon - lonDelta && p.Longitude <= lon + lonDelta)
            .Select(p => new { p.Id, p.Latitude, p.Longitude })
            .ToListAsync(ct);

        if (candidates.Count == 0)
            return Result<NearbyPropertiesDto>.Success(new NearbyPropertiesDto(
                [], true, true, locationName, NearbyModeDistance));

        var ranked = candidates
            .Select(c => new
            {
                c.Id,
                DistanceKm = HaversineKm(lat, lon, c.Latitude!.Value, c.Longitude!.Value)
            })
            .OrderBy(c => c.DistanceKm)
            .ThenBy(c => c.Id)
            .Take(NearbyResultLimit)
            .ToList();

        // Hydrate only the rows that survived, then restore the distance order,
        // because the projection query has no ordering of its own.
        var order = ranked.Select((r, i) => new { r.Id, i }).ToDictionary(x => x.Id, x => x.i);
        var rows = await Project(query.Where(p => order.ContainsKey(p.Id)), currentUserId)
            .ToListAsync(ct);

        var byId = rows.ToDictionary(r => r.Id);
        var items = new List<NearbyPropertyDto>(ranked.Count);

        foreach (var entry in ranked.OrderBy(e => order[e.Id]))
        {
            if (!byId.TryGetValue(entry.Id, out var row))
                continue;

            items.Add(new NearbyPropertyDto(
                ToDto(row),
                Math.Round(entry.DistanceKm, 1),
                FormatDistance(entry.DistanceKm, row.Area, row.City)));
        }

        return Result<NearbyPropertiesDto>.Success(new NearbyPropertiesDto(
            items, true, true, locationName, NearbyModeDistance));
    }

    private async Task<Result<NearbyPropertiesDto>> AreaMatchAsync(
        IQueryable<Property> query, string locationName, string currentUserId, CancellationToken ct)
    {
        // The user picked an area from the Location hierarchy but did not share
        // coordinates. Properties store their area as text, not as a LocationId, so
        // the match is on the state/city name. This is an area match, not a distance:
        // no kilometre figure is produced, and the UI is told so through
        // DistanceAvailable = false.
        var isCity = await dbContext.Locations
            .AsNoTracking()
            .AnyAsync(l => l.Name == locationName && l.Type == LocationType.City, ct);

        var target = locationName.Trim().ToLowerInvariant();
        if (target.Length == 0)
            return Result<NearbyPropertiesDto>.Success(new NearbyPropertiesDto(
                [], true, false, locationName, NearbyModeArea));

        // "Plateau" and "Plateau State" must both match, so both spellings are
        // compared. These stay translatable to SQL (Trim/Lower map to btrim/lower);
        // the shared NormalizeLocation helper cannot be used here because EF cannot
        // translate a C# method into the query.
        var withState = target.EndsWith(" state", StringComparison.Ordinal)
            ? target[..^6]
            : target + " state";

        IQueryable<Property> areaQuery = isCity
            ? query.Where(p => p.City.Trim().ToLower() == target || p.City.Trim().ToLower() == withState)
            : query.Where(p => p.State.Trim().ToLower() == target || p.State.Trim().ToLower() == withState);

        var rows = await Project(areaQuery.OrderByDescending(p => p.CreatedAt), currentUserId)
            .Take(NearbyResultLimit)
            .ToListAsync(ct);

        return Result<NearbyPropertiesDto>.Success(new NearbyPropertiesDto(
            rows.Select(r => new NearbyPropertyDto(ToDto(r), null, null)).ToList(),
            true, false, locationName, NearbyModeArea));
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        const double toRad = Math.PI / 180.0;

        var dLat = (lat2 - lat1) * toRad;
        var dLon = (lon2 - lon1) * toRad;
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
            + Math.Cos(lat1 * toRad) * Math.Cos(lat2 * toRad) * Math.Pow(Math.Sin(dLon / 2), 2);

        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static string FormatDistance(double km, string? area, string? city)
    {
        // The coordinates in this database are neighbourhood-level seed values, not
        // per-address geocoding: several different properties share one exact pair.
        // Reporting "0 m away" for those would be false precision, so a sub-100 m
        // result is labelled by the area the property actually declares instead.
        if (km < 0.1)
        {
            var label = string.IsNullOrWhiteSpace(area) ? city : area;
            return string.IsNullOrWhiteSpace(label) ? "In your area" : label.Trim();
        }

        if (km < 10)
            return $"{Math.Round(km, 1)} km away";

        return $"{Math.Round(km, 0)} km away";
    }

    public async Task<Result<PropertyDto>> CreateAsync(CreatePropertyRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var agentIdResult = await ResolveAgentIdAsync(request.AgentId, currentUserId, currentUserRoles, ct);
        if (agentIdResult.IsFailure)
            return Result<PropertyDto>.Failure(agentIdResult.Error);

        if (!TryResolveType(request.Type ?? request.PropertyType, out var propertyType))
            return Result<PropertyDto>.Failure(
                Error.Validation("property.invalidtype", $"'{request.Type ?? request.PropertyType}' is not a valid property type."));

        if (!TryResolveListing(request.Status, request.ListingType, PropertyStatus.Available, out var listingType, out var status))
            return Result<PropertyDto>.Failure(
                Error.Validation("property.invalidstatus", $"'{request.Status ?? request.ListingType}' is not a valid listing status."));

        var slug = await EnsureUniqueSlugAsync(request.Slug, request.Title, ct);

        // Single source of truth: when a LocationId is supplied, State/City are
        // derived from the location hierarchy rather than accepted from the
        // client, so the two representations cannot drift apart.
        var location = await ResolveLocationAsync(request.LocationId, ct);
        if (location.IsFailure)
            return Result<PropertyDto>.Failure(location.Error);

        var state = location.Value?.State ?? request.State;
        var city = location.Value?.City ?? request.City;

        var property = new Property
        {
            Title = request.Title,
            Description = request.Description,
            Slug = slug,
            Price = request.Price,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "NGN" : request.Currency,
            Period = request.Period,
            PropertyType = propertyType,
            ListingType = listingType,
            Status = status,
            Bedrooms = request.Bedrooms,
            Bathrooms = request.Bathrooms,
            Size = request.Size,
            SizeUnit = string.IsNullOrWhiteSpace(request.SizeUnit) ? "sqm" : request.SizeUnit,
            LotSize = request.LotSize,
            YearBuilt = request.YearBuilt,
            Address = request.Address,
            State = state,
            City = city,
            Area = request.Area,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Amenities = request.Amenities ?? [],
            Featured = request.Featured,
            AgentId = agentIdResult.Value,
            LocationId = location.Value?.Id,
            CreatedByUserId = currentUserId,
            CreatedAt = DateTime.UtcNow,
            PropertyImages = BuildImages(request.Images)
        };

        dbContext.Properties.Add(property);
        await dbContext.SaveChangesAsync(ct);

        var created = await LoadPropertyAsync(property.Id, true, ct);
        return Result<PropertyDto>.Success(created!.ToDto(enquiryCount: 0));
    }

    public async Task<Result<PropertyDto>> UpdateAsync(int id, UpdatePropertyRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var property = await LoadPropertyAsync(id, true, ct);
        if (property is null)
            return Result<PropertyDto>.Failure(
                Error.NotFound("property.notfound", $"Property with id {id} was not found."));

        var ownership = await VerifyOwnershipAsync(property, currentUserId, currentUserRoles, ct);
        if (ownership.IsFailure)
            return Result<PropertyDto>.Failure(ownership.Error);

        if (!TryResolveType(request.Type ?? request.PropertyType, out var propertyType))
            return Result<PropertyDto>.Failure(
                Error.Validation("property.invalidtype", $"'{request.Type ?? request.PropertyType}' is not a valid property type."));

        if (!TryResolveListing(request.Status, request.ListingType, property.Status, out var listingType, out var status))
            return Result<PropertyDto>.Failure(
                Error.Validation("property.invalidstatus", $"'{request.Status ?? request.ListingType}' is not a valid listing status."));

        if (currentUserRoles.Contains(Roles.Admin) && request.AgentId.HasValue)
        {
            if (!await dbContext.Agents.AnyAsync(a => a.Id == request.AgentId.Value, ct))
                return Result<PropertyDto>.Failure(
                    Error.Validation("property.invalidagent", $"Agent with id {request.AgentId.Value} does not exist."));
            property.AgentId = request.AgentId.Value;
        }

        property.Title = request.Title;
        property.Description = request.Description;
        property.Slug = await EnsureUniqueSlugAsync(request.Slug, request.Title, ct, excludeId: id);
        property.Price = request.Price;
        property.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "NGN" : request.Currency;
        property.Period = request.Period;
        property.PropertyType = propertyType;
        property.ListingType = listingType;
        property.Status = status;
        property.Bedrooms = request.Bedrooms;
        property.Bathrooms = request.Bathrooms;
        property.Size = request.Size;
        property.SizeUnit = string.IsNullOrWhiteSpace(request.SizeUnit) ? "sqm" : request.SizeUnit;
        property.LotSize = request.LotSize;
        property.YearBuilt = request.YearBuilt;
        var location = await ResolveLocationAsync(request.LocationId, ct);
        if (location.IsFailure)
            return Result<PropertyDto>.Failure(location.Error);

        property.Address = request.Address;
        property.State = location.Value?.State ?? request.State;
        property.City = location.Value?.City ?? request.City;
        property.Area = request.Area;
        property.Latitude = request.Latitude;
        property.Longitude = request.Longitude;
        property.Amenities = request.Amenities ?? [];
        property.Featured = request.Featured;
        property.LocationId = location.Value?.Id;
        property.UpdatedAt = DateTime.UtcNow;

        if (request.Images is not null)
        {
            property.PropertyImages.Clear();
            foreach (var image in BuildImages(request.Images))
                property.PropertyImages.Add(image);
        }

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PropertyDto>.Failure(Error.Concurrency());
        }

        var updated = await LoadPropertyAsync(id, true, ct);
        return Result<PropertyDto>.Success(updated!.ToDto(enquiryCount: await EnquiryCountAsync(id, ct)));
    }

    public async Task<Result<PropertyDto>> SetFeaturedAsync(int id, bool featured, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var property = await LoadPropertyAsync(id, true, ct);
        if (property is null)
            return Result<PropertyDto>.Failure(
                Error.NotFound("property.notfound", $"Property with id {id} was not found."));

        var ownership = await VerifyOwnershipAsync(property, currentUserId, currentUserRoles, ct);
        if (ownership.IsFailure)
            return Result<PropertyDto>.Failure(ownership.Error);

        property.Featured = featured;
        property.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PropertyDto>.Failure(Error.Concurrency());
        }

        var updated = await LoadPropertyAsync(id, true, ct);
        return Result<PropertyDto>.Success(updated!.ToDto(enquiryCount: await EnquiryCountAsync(id, ct)));
    }

    public async Task<Result> DeleteAsync(int id, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var property = await dbContext.Properties.FindAsync([id], ct);
        if (property is null)
            return Result.Failure(
                Error.NotFound("property.notfound", $"Property with id {id} was not found."));

        var ownership = await VerifyOwnershipAsync(property, currentUserId, currentUserRoles, ct);
        if (ownership.IsFailure)
            return Result.Failure(ownership.Error);

        dbContext.Properties.Remove(property);

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Concurrency());
        }
        catch (DbUpdateException)
        {
            return Result.Failure(
                Error.Conflict("property.hasrecords", "Cannot delete a property with an existing sale or lease record."));
        }

        return Result.Success();
    }

    // =========================================================
    // Items 1, 3, 4, 6, 7 — Image remove, Status, ListingType, Agent assign
    // =========================================================

    public async Task<Result> RemoveImageAsync(int propertyId, string publicId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var property = await LoadPropertyAsync(propertyId, true, ct);
        if (property is null)
            return Result.Failure(
                Error.NotFound("property.notfound", $"Property with id {propertyId} was not found."));

        var ownership = await VerifyOwnershipAsync(property, currentUserId, currentUserRoles, ct);
        if (ownership.IsFailure)
            return Result.Failure(ownership.Error);

        var image = property.PropertyImages.FirstOrDefault(i => i.PublicId == publicId);
        if (image is null)
            return Result.Failure(
                Error.NotFound("property.imagenotfound", $"Image with publicId '{publicId}' was not found on this property."));

        if (!string.IsNullOrWhiteSpace(image.PublicId) && !image.PublicId.StartsWith("existing-"))
        {
            try { await imageService.DeleteAsync(image.PublicId, ct); }
            catch { /* best-effort cleanup */ }
        }

        property.PropertyImages.Remove(image);
        property.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Concurrency());
        }

        return Result.Success();
    }

    public async Task<Result<PropertyDto>> ChangeStatusAsync(int id, string status, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var property = await LoadPropertyAsync(id, true, ct);
        if (property is null)
            return Result<PropertyDto>.Failure(
                Error.NotFound("property.notfound", $"Property with id {id} was not found."));

        var ownership = await VerifyOwnershipAsync(property, currentUserId, currentUserRoles, ct);
        if (ownership.IsFailure)
            return Result<PropertyDto>.Failure(ownership.Error);

        if (!TryResolveStatus(status, out var newStatus))
            return Result<PropertyDto>.Failure(
                Error.Validation("property.invalidstatus", $"'{status}' is not a valid property status."));

        property.Status = newStatus;
        property.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PropertyDto>.Failure(Error.Concurrency());
        }

        var updated = await LoadPropertyAsync(id, true, ct);
        return Result<PropertyDto>.Success(updated!.ToDto(enquiryCount: await EnquiryCountAsync(id, ct)));
    }

    public async Task<Result<PropertyDto>> ChangeListingTypeAsync(int id, string listingType, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var property = await LoadPropertyAsync(id, true, ct);
        if (property is null)
            return Result<PropertyDto>.Failure(
                Error.NotFound("property.notfound", $"Property with id {id} was not found."));

        var ownership = await VerifyOwnershipAsync(property, currentUserId, currentUserRoles, ct);
        if (ownership.IsFailure)
            return Result<PropertyDto>.Failure(ownership.Error);

        if (!Enum.TryParse<ListingType>(listingType, true, out var newListingType))
            return Result<PropertyDto>.Failure(
                Error.Validation("property.invalidlistingtype", $"'{listingType}' is not a valid listing type."));

        property.ListingType = newListingType;
        property.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PropertyDto>.Failure(Error.Concurrency());
        }

        var updated = await LoadPropertyAsync(id, true, ct);
        return Result<PropertyDto>.Success(updated!.ToDto(enquiryCount: await EnquiryCountAsync(id, ct)));
    }

    public async Task<Result<PropertyDto>> AssignAgentAsync(int id, int? agentId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (!currentUserRoles.Contains(Roles.Admin))
            return Result<PropertyDto>.Failure(
                Error.Forbidden("property.forbidden", "Only admins can assign or unassign agents."));

        var property = await LoadPropertyAsync(id, true, ct);
        if (property is null)
            return Result<PropertyDto>.Failure(
                Error.NotFound("property.notfound", $"Property with id {id} was not found."));

        if (agentId.HasValue)
        {
            if (!await dbContext.Agents.AnyAsync(a => a.Id == agentId.Value, ct))
                return Result<PropertyDto>.Failure(
                    Error.Validation("property.invalidagent", $"Agent with id {agentId.Value} does not exist."));
        }

        property.AgentId = agentId;
        property.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PropertyDto>.Failure(Error.Concurrency());
        }

        var updated = await LoadPropertyAsync(id, true, ct);
        return Result<PropertyDto>.Success(updated!.ToDto(enquiryCount: await EnquiryCountAsync(id, ct)));
    }

    // =========================================================
    // Helpers
    // =========================================================

    /// <summary>
    /// Resolves a client-supplied LocationId to the State/City names stored in
    /// the location hierarchy.
    ///
    /// Free text stays supported (the current form collects State and City names
    /// inline and existing rows have no LocationId), but when a LocationId IS
    /// supplied it wins, so the structured location and the denormalised text
    /// columns can never disagree. Returns a null value when no LocationId was
    /// sent, meaning "keep the client's free text".
    /// </summary>
    private async Task<Result<ResolvedLocation?>> ResolveLocationAsync(int? locationId, CancellationToken ct)
    {
        if (!locationId.HasValue)
            return Result<ResolvedLocation?>.Success(null);

        var location = await dbContext.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == locationId.Value, ct);

        if (location is null)
            return Result<ResolvedLocation?>.Failure(
                Error.Validation("property.locationnotfound", $"Location with id {locationId.Value} was not found."));

        // Walk to the root so the state is correct even when the client selected
        // a City or Area node. The hierarchy is only 4 levels deep.
        var stateName = location.Name;
        var cityName = location.Name;
        var currentId = location.ParentId;
        var isCityLevel = location.Type == LocationType.City || location.Type == LocationType.Area;

        var chain = new List<Location> { location };
        while (currentId.HasValue)
        {
            var parent = await dbContext.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == currentId.Value, ct);

            if (parent is null)
                break;

            chain.Add(parent);
            currentId = parent.ParentId;
        }

        var root = chain.Last();
        stateName = root.Type == LocationType.State ? root.Name : location.Name;
        cityName = isCityLevel && chain.Count >= 2 ? chain[chain.Count - 2].Name : location.Name;

        return Result<ResolvedLocation?>.Success(new ResolvedLocation(location.Id, stateName, cityName));
    }

    private sealed record ResolvedLocation(int Id, string State, string City);

    private static string NormalizeLocation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().ToLowerInvariant();
        const string stateSuffix = " state";

        if (normalized.EndsWith(stateSuffix, StringComparison.Ordinal))
            normalized = normalized[..^stateSuffix.Length].Trim();

        return normalized;
    }

    private static IQueryable<Property> ApplyStatusFilter(IQueryable<Property> query, string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return query;

        return status.Trim() switch
        {
            "Available" => query.Where(p => p.Status == PropertyStatus.Available),
            "Pending" => query.Where(p => p.Status == PropertyStatus.Pending),
            "Sold" => query.Where(p => p.Status == PropertyStatus.Sold),
            "Rented" or "Leased" => query.Where(p => p.Status == PropertyStatus.Rented),
            "Unavailable" or "Withdrawn" or "OffMarket" or "Off Market" => query.Where(p =>
                p.Status == PropertyStatus.Unavailable),
            _ => Enum.TryParse<PropertyStatus>(status, true, out var parsed)
                ? query.Where(p => p.Status == parsed)
                : query
        };
    }

    private static IQueryable<Property> ApplySorting(IQueryable<Property> query, PropertyQueryParameters q)
    {
        if (!string.IsNullOrWhiteSpace(q.Sort))
        {
            return q.Sort.Trim().ToLower() switch
            {
                "price-asc" => query.OrderBy(p => p.Price),
                "price-desc" => query.OrderByDescending(p => p.Price),
                "popular" => query.OrderByDescending(p => p.Featured).ThenByDescending(p => p.CreatedAt),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };
        }

        return q.SortBy?.ToLower() switch
        {
            "price" => q.SortDescending ? query.OrderByDescending(p => p.Price)
                                        : query.OrderBy(p => p.Price),
            "title" => q.SortDescending ? query.OrderByDescending(p => p.Title)
                                        : query.OrderBy(p => p.Title),
            _ => q.SortDescending ? query.OrderByDescending(p => p.CreatedAt)
                                  : query.OrderBy(p => p.CreatedAt)
        };
    }

    private IQueryable<PropertyProjection> Project(IQueryable<Property> query, string? currentUserId) =>
        query.Select(p => new PropertyProjection(
            p.Id,
            p.Title,
            p.Slug,
            p.Description,
            p.Price,
            p.Currency,
            p.Period,
            p.Status,
            p.PropertyType,
            p.ListingType,
            p.Bedrooms,
            p.Bathrooms,
            p.Size,
            p.SizeUnit,
            p.LotSize,
            p.YearBuilt,
            p.Address,
            p.City,
            p.Area,
            p.State,
            p.Latitude,
            p.Longitude,
            p.LocationId,
            p.PropertyImages.OrderBy(i => i.DisplayOrder).Select(i => i.Url).ToList(),
            p.PropertyImages.Where(i => i.IsCover).Select(i => i.Url).FirstOrDefault()
                ?? p.PropertyImages.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
            p.Amenities,
            p.Featured,
            p.AgentId,
            p.Agent != null ? p.Agent.User.FullName : null,
            p.Agent != null ? p.Agent.PhotoUrl : null,
            currentUserId == null ? false : p.SavedByUsers.Any(s => s.UserId == currentUserId),
            p.Enquiries.Count(),
            p.CreatedAt,
            p.UpdatedAt));

    private static PropertyDto ToDto(PropertyProjection p) =>
        new(
            p.Id,
            p.Title,
            p.Slug,
            p.Description,
            p.Price,
            p.Currency,
            p.Period,
            PropertyStatusDisplay.ToFrontend(p.Status),
            PropertyTypeDisplay.ToFrontend(p.PropertyType),
            p.PropertyType.ToString(),
            p.ListingType.ToString(),
            p.Bedrooms,
            p.Bathrooms,
            p.Size,
            p.SizeUnit,
            p.LotSize,
            p.YearBuilt,
            p.Address,
            p.City,
            p.Area,
            p.State,
            p.Latitude,
            p.Longitude,
            p.LocationId,
            p.Images,
            p.CoverImage,
            p.Amenities,
            p.Featured,
            p.AgentId,
            p.AgentName,
            p.AgentPhoto,
            p.IsSaved,
            p.EnquiryCount,
            p.CreatedAt,
            p.UpdatedAt);

    private async Task<Property?> LoadPropertyAsync(int id, bool includeSuspendedAgents, CancellationToken ct)
    {
        IQueryable<Property> query = dbContext.Properties
            .Include(p => p.Agent)
                .ThenInclude(a => a!.User)
            .Include(p => p.PropertyImages);

        if (!includeSuspendedAgents)
            query = query.VisibleProperties();

        return await query.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    private async Task<bool> IsSavedAsync(int propertyId, string? currentUserId, CancellationToken ct)
    {
        if (currentUserId is null)
            return false;

        return await dbContext.SavedProperties.AnyAsync(s => s.PropertyId == propertyId && s.UserId == currentUserId, ct);
    }

    private Task<int> EnquiryCountAsync(int propertyId, CancellationToken ct) =>
        dbContext.Enquiries.CountAsync(e => e.PropertyId == propertyId, ct);

    private async Task<Result<int?>> ResolveAgentIdAsync(int? requestedAgentId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (currentUserRoles.Contains(Roles.Agent))
        {
            var agent = await dbContext.Agents.FirstOrDefaultAsync(a => a.UserId == currentUserId, ct);
            if (agent is null)
                return Result<int?>.Failure(
                    Error.Validation("property.nolinkedagent", "Your account has no linked agent profile — contact an administrator."));

            // A suspended agent may keep browsing and reading, but must not be
            // able to publish new inventory. Checked here so every create path
            // inherits it without repeating the rule.
            if (agent.IsSuspended)
                return Result<int?>.Failure(
                    Error.Forbidden("property.agentsuspended", "Your account is suspended. You cannot create properties until an administrator reinstates it."));

            return Result<int?>.Success(agent.Id);
        }

        if (currentUserRoles.Contains(Roles.Admin))
        {
            if (!requestedAgentId.HasValue)
                return Result<int?>.Success(null);

            if (!await dbContext.Agents.AnyAsync(a => a.Id == requestedAgentId.Value, ct))
                return Result<int?>.Failure(
                    Error.Validation("property.invalidagent", "A valid agentId is required when an admin creates a property."));

            return Result<int?>.Success(requestedAgentId.Value);
        }

        return Result<int?>.Failure(
            Error.Unauthorized("property.unauthorized", "You are not authorized to create a property."));
    }

    private async Task<Result> VerifyOwnershipAsync(Property property, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (currentUserRoles.Contains(Roles.Admin))
            return Result.Success();

        if (currentUserRoles.Contains(Roles.Agent))
        {
            var agent = await dbContext.Agents.FirstOrDefaultAsync(a => a.UserId == currentUserId, ct);
            if (agent is null || property.AgentId != agent.Id)
                return Result.Failure(
                    Error.Forbidden("property.forbidden", "You cannot modify a property that is not assigned to you."));

            // Admins are exempt above and still moderate suspended inventory. An
            // agent's own suspension blocks every mutation they would otherwise
            // own, which covers update, delete, status change and image actions
            // because they all resolve ownership through this method.
            if (agent.IsSuspended)
                return Result.Failure(
                    Error.Forbidden("property.agentsuspended", "Your account is suspended. You cannot manage your properties until an administrator reinstates it."));

            return Result.Success();
        }

        return Result.Failure(
            Error.Unauthorized("property.unauthorized", "You are not authorized to manage properties."));
    }

    private static bool TryResolveType(string? type, out PropertyType propertyType) =>
        PropertyTypeDisplay.TryParse(type, out propertyType);

    private static bool TryResolveStatus(string? statusLabel, out PropertyStatus status)
    {
        status = PropertyStatus.Available;
        if (string.IsNullOrWhiteSpace(statusLabel))
            return true;

        return statusLabel.Trim() switch
        {
            "Available" or "ForSale" or "For Sale" => (status = PropertyStatus.Available, true).Item2,
            "Pending" => (status = PropertyStatus.Pending, true).Item2,
            "Sold" => (status = PropertyStatus.Sold, true).Item2,
            "Rented" or "Leased" => (status = PropertyStatus.Rented, true).Item2,
            "Unavailable" or "Withdrawn" or "OffMarket" or "Off Market" => (status = PropertyStatus.Unavailable, true).Item2,
            _ => Enum.TryParse<PropertyStatus>(statusLabel, true, out status)
        };
    }

    private static bool TryResolveListing(string? statusLabel, string? listingType, PropertyStatus fallbackStatus, out ListingType listingTypeOut, out PropertyStatus status)
    {
        listingTypeOut = ListingType.ForSale;
        status = fallbackStatus;

        if (!string.IsNullOrWhiteSpace(listingType))
        {
            if (!Enum.TryParse<ListingType>(listingType, true, out var parsed))
                return false;
            listingTypeOut = parsed;
        }

        if (!string.IsNullOrWhiteSpace(statusLabel))
        {
            switch (statusLabel.Trim())
            {
                case "Available" or "ForSale" or "For Sale":
                    status = PropertyStatus.Available;
                    return true;
                case "Pending":
                    status = PropertyStatus.Pending;
                    return true;
                case "Sold":
                    status = PropertyStatus.Sold;
                    return true;
                case "Rented" or "Leased":
                    status = PropertyStatus.Rented;
                    return true;
                case "Unavailable" or "Withdrawn" or "OffMarket" or "Off Market":
                    status = PropertyStatus.Unavailable;
                    return true;
                default:
                    return Enum.TryParse<PropertyStatus>(statusLabel, true, out status);
            }
        }

        return true;
    }

    private static List<PropertyImage> BuildImages(List<string>? urls)
    {
        if (urls is null || urls.Count == 0)
            return [];

        return urls
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select((url, index) => new PropertyImage
            {
                Url = url,
                PublicId = string.Empty,
                IsCover = index == 0,
                DisplayOrder = index
            })
            .ToList();
    }

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.Trim().ToLower(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length == 0 ? "property" : slug;
    }

    private async Task<string> EnsureUniqueSlugAsync(string? slug, string title, CancellationToken ct, int? excludeId = null)
    {
        var baseSlug = Slugify(string.IsNullOrWhiteSpace(slug) ? title : slug);
        var candidate = baseSlug;
        var suffix = 2;

        while (await dbContext.Properties.AnyAsync(p => p.Slug == candidate && (excludeId == null || p.Id != excludeId), ct))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private sealed record PropertyProjection(
        int Id,
        string Title,
        string Slug,
        string Description,
        decimal Price,
        string Currency,
        string? Period,
        PropertyStatus Status,
        PropertyType PropertyType,
        ListingType ListingType,
        int? Bedrooms,
        int? Bathrooms,
        double? Size,
        string SizeUnit,
        double? LotSize,
        int? YearBuilt,
        string Address,
        string City,
        string? Area,
        string State,
        double? Latitude,
        double? Longitude,
        int? LocationId,
        IReadOnlyList<string> Images,
        string? CoverImage,
        List<string> Amenities,
        bool Featured,
        int? AgentId,
        string? AgentName,
        string? AgentPhoto,
        bool IsSaved,
        int EnquiryCount,
        DateTime CreatedAt,
        DateTime? UpdatedAt);
}
