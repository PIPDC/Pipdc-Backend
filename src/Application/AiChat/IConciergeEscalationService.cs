using PIPDC.Application.Common;
using PIPDC.Domain.Common;

namespace PIPDC.Application.AiChat;

public interface IConciergeEscalationService
{
    Task<Result<PaginatedResult<ConciergeEscalationDto>>> GetEscalationsAsync(
        string currentUserId, IList<string> currentUserRoles, ConciergeEscalationQueryParameters q, CancellationToken ct);

    Task<Result<ConciergeEscalationDetailDto>> GetByIdAsync(
        int id, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    Task<Result<ConciergeEscalationDto>> ClaimAsync(
        int id, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    Task<Result<ConciergeEscalationDto>> ResolveAsync(
        int id, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);
}