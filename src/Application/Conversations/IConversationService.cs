using PIPDC.Application.Common;
using PIPDC.Domain.Common;

namespace PIPDC.Application.Conversations;

public interface IConversationService
{
    Task<Result<EnquiryConversationStateDto>> GetStateByEnquiryAsync(int enquiryId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    Task<Result<PaginatedResult<ConversationDto>>> GetMineAsync(string currentUserId, IList<string> currentUserRoles, ConversationQueryParameters queryParams, CancellationToken ct);

    Task<Result<ConversationDto>> GetByIdAsync(int id, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    // Starts (or returns the existing) conversation between the current client
    // and the property's agent. Idempotent on (client, agent, property).
    Task<Result<StartConversationResultDto>> StartAsync(StartConversationRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);
}
