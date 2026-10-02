using PIPDC.Application.Common;
using PIPDC.Domain.Common;

namespace PIPDC.Application.Conversations;

/// <summary>
/// Batch 6: handing a client conversation from its handling agent to PIPDC.
/// <para>
/// Every method reuses the existing <see cref="Conversation"/> and
/// <see cref="Message"/> rows. Escalation only records who owns the issue and
/// why; it never moves, copies, forks, or deletes a single message, so the
/// client's history stays exactly one thread with exactly one id.
/// </para>
/// </summary>
public interface IConversationEscalationService
{
    /// <summary>Admin queue: escalated but unclaimed, plus cases this admin owns.</summary>
    Task<Result<PaginatedResult<ConversationDto>>> GetEscalationsAsync(
        string currentUserId, IList<string> currentUserRoles, ConversationQueryParameters queryParams, CancellationToken ct);

    /// <summary>Agent hands the conversation to PIPDC. Records who, when, and why.</summary>
    Task<Result<ConversationDto>> EscalateAsync(
        int conversationId, EscalateConversationRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    /// <summary>An administrator takes ownership, so only they may reply from then on.</summary>
    Task<Result<ConversationDto>> ClaimAsync(
        int conversationId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    /// <summary>The owning administrator closes the case. History is retained.</summary>
    Task<Result<ConversationDto>> ResolveAsync(
        int conversationId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);
}
