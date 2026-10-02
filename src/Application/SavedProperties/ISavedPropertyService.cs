using PIPDC.Application.Common;
using PIPDC.Domain.Common;

namespace PIPDC.Application.SavedProperties;

public interface ISavedPropertyService
{
    Task<Result<PaginatedResult<SavedPropertyDto>>> GetSavedAsync(string userId, bool includeSuspendedAgents, SavedPropertyQueryParameters q, CancellationToken ct);
    Task<Result<IReadOnlyList<int>>> GetSavedIdsAsync(string userId, bool includeSuspendedAgents, CancellationToken ct);
    Task<Result> SaveAsync(string userId, int propertyId, bool includeSuspendedAgents, CancellationToken ct);
    Task<Result> UnsaveAsync(string userId, int propertyId, CancellationToken ct);
}
