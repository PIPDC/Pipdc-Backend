using PIPDC.Application.Common;
using PIPDC.Domain.Common;

namespace PIPDC.Application.Properties;

public interface IPropertyService
{
    // The includeSuspendedAgents flag on the public reads is supplied by the API
    // layer from the caller's own role. It is never bound from a query string, so
    // a public caller cannot ask to see properties owned by a suspended agent.
    Task<Result<PaginatedResult<PropertyDto>>> GetAllAsync(
        PropertyQueryParameters queryParams, string? currentUserId, bool includeSuspendedAgents, CancellationToken ct);
    Task<Result<PropertyDto>> GetByIdAsync(int id, string? currentUserId, bool includeSuspendedAgents, CancellationToken ct);
    Task<Result<PropertyDto>> GetBySlugAsync(string slug, string? currentUserId, bool includeSuspendedAgents, CancellationToken ct);
    Task<Result<IReadOnlyList<PropertyDto>>> GetFeaturedAsync(string? currentUserId, bool includeSuspendedAgents, CancellationToken ct);
    Task<Result<IReadOnlyList<PropertyDto>>> GetSimilarAsync(int id, string? currentUserId, bool includeSuspendedAgents, CancellationToken ct);
    // Batch 5. Always resolved from the signed-in user; no user id is accepted from
    // the caller. Visibility is applied inside the service rather than trusting the
    // API layer, because a nearby list is a public surface.
    Task<Result<NearbyPropertiesDto>> GetNearbyAsync(string currentUserId, CancellationToken ct);
    Task<Result<PropertyDto>> CreateAsync(CreatePropertyRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);
    Task<Result<PropertyDto>> UpdateAsync(int id, UpdatePropertyRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);
    Task<Result<PropertyDto>> SetFeaturedAsync(int id, bool featured, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);
    Task<Result> DeleteAsync(int id, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);
    Task<Result> RemoveImageAsync(int propertyId, string publicId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);
    Task<Result<PropertyDto>> ChangeStatusAsync(int id, string status, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);
    Task<Result<PropertyDto>> ChangeListingTypeAsync(int id, string listingType, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);
    Task<Result<PropertyDto>> AssignAgentAsync(int id, int? agentId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);
}
