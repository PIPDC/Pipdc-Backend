using Microsoft.EntityFrameworkCore;
using PIPDC.Application.Auth;
using PIPDC.Application.Data;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Conversations;

// Centralized authorization for conversation/enquiry access.
// Admin can inspect any conversation but is never treated as a sender participant.
//
// Public because MessagingHub (PIPDC.API) reuses these same rules when a client
// joins a conversation group. It was internal while the solution was one
// assembly; the project split made that visible. API -> Application is a
// permitted direction, so the fix is visibility, not a new dependency.
public static class ConversationAuthorization
{
    public static async Task<bool> CanAccessConversationAsync(
        IAppDbContext dbContext, Conversation conversation, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (currentUserRoles.Contains(Roles.Admin))
            return true;

        return await IsParticipantAsync(dbContext, conversation, currentUserId, ct);
    }

    public static async Task<bool> IsParticipantAsync(
        IAppDbContext dbContext, Conversation conversation, string currentUserId, CancellationToken ct)
    {
        if (conversation.ClientUserId == currentUserId)
            return true;

        // The agent linked to the conversation (the agent who manages the enquiry's property).
        return await dbContext.Agents.AnyAsync(
            a => a.Id == conversation.AgentId && a.UserId == currentUserId, ct);
    }

    public static async Task<bool> IsEnquiryParticipantAsync(
        IAppDbContext dbContext, Enquiry enquiry, string currentUserId, CancellationToken ct)
    {
        if (enquiry.UserId == currentUserId)
            return true;

        // The agent who manages the enquiry's property.
        return await dbContext.Properties.AnyAsync(
            p => p.Id == enquiry.PropertyId && p.Agent != null && p.Agent.UserId == currentUserId, ct);
    }

    public static async Task<bool> CanAccessEnquiryAsync(
        IAppDbContext dbContext, Enquiry enquiry, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (currentUserRoles.Contains(Roles.Admin))
            return true;

        if (enquiry.UserId == currentUserId)
            return true;

        // The agent who manages the enquiry's property.
        return await dbContext.Properties.AnyAsync(
            p => p.Id == enquiry.PropertyId && p.Agent != null && p.Agent.UserId == currentUserId, ct);
    }

    /// <summary>
    /// Whether the caller may hand this conversation to PIPDC.
    /// <para>
    /// Only the agent who actually owns the conversation can escalate it. A
    /// client must never be able to escalate, because that would be a request to
    /// assign staff to their own thread, and an admin has no need to escalate to
    /// themselves since admins can already claim any conversation.
    /// </para>
    /// </summary>
    public static async Task<Result> AuthorizeEscalateAsync(
        IAppDbContext dbContext, Conversation conversation, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (currentUserRoles.Contains(Roles.Admin))
            return Result.Failure(
                Error.Forbidden("escalation.adminnotpermitted", "Administrators handle escalations directly, so there is nothing to escalate."));

        if (!currentUserRoles.Contains(Roles.Agent))
            return Result.Failure(
                Error.Forbidden("escalation.forbidden", "Only the agent handling this conversation can escalate it."));

        if (conversation.EscalationStatus is ConversationEscalationStatus.Assigned)
            return Result.Failure(
                Error.Conflict("escalation.alreadyassigned", "An administrator already owns this conversation."));

        if (conversation.EscalationStatus is ConversationEscalationStatus.Resolved)
            return Result.Failure(
                Error.Conflict("escalation.resolved", "This conversation is already resolved."));

        // Must be the agent linked to this conversation, not merely any agent.
        var agent = await dbContext.Agents
            .FirstOrDefaultAsync(a => a.Id == conversation.AgentId && a.UserId == currentUserId, ct);

        if (agent is null)
            return Result.Failure(
                Error.Forbidden("escalation.forbidden", "Only the agent handling this conversation can escalate it."));

        if (agent.IsSuspended)
            return Result.Failure(
                Error.Forbidden("escalation.agentsuspended", "Your account is suspended, so you cannot escalate conversations."));

        return Result.Success();
    }

    /// <summary>
    /// Whether the caller may take ownership of an escalated conversation.
    /// Admin-only, and only while nobody has claimed it.
    /// </summary>
    public static Result AuthorizeClaim(Conversation conversation, IList<string> currentUserRoles)
    {
        if (!currentUserRoles.Contains(Roles.Admin))
            return Result.Failure(
                Error.Forbidden("escalation.admironly", "Only an administrator can take ownership of an escalated conversation."));

        if (conversation.EscalationStatus is not ConversationEscalationStatus.Escalated)
            return Result.Failure(
                Error.Conflict("escalation.notawaitingadmin", "This conversation is not awaiting an administrator."));

        return Result.Success();
    }

    /// <summary>
    /// Only the administrator who owns the issue may resolve it. An admin who is
    /// merely browsing the queue must not be able to close somebody else's case.
    /// </summary>
    public static Result AuthorizeResolve(Conversation conversation, string currentUserId, IList<string> currentUserRoles)
    {
        if (!currentUserRoles.Contains(Roles.Admin))
            return Result.Failure(
                Error.Forbidden("escalation.admironly", "Only an administrator can resolve an escalated conversation."));

        if (conversation.EscalationStatus is not ConversationEscalationStatus.Assigned)
            return Result.Failure(
                Error.Conflict("escalation.notassigned", "You must take ownership of this conversation before resolving it."));

        if (conversation.AssignedAdminId != currentUserId)
            return Result.Failure(
                Error.Forbidden("escalation.notyourcase", "Another administrator owns this conversation."));

        return Result.Success();
    }

    /// <summary>
    /// Whether the caller may post a message in this conversation.
    /// <para>
    /// This is the rule that stops two people writing into the client's thread at
    /// once: once an admin is assigned, the original agent is read-only until the
    /// issue is resolved. The client can always still write to PIPDC.
    /// </para>
    /// </summary>
    public static async Task<Result> AuthorizeSendAsync(
        IAppDbContext dbContext, Conversation conversation, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (conversation.EscalationStatus is ConversationEscalationStatus.Resolved)
            return Result.Failure(
                Error.Conflict("message.conversationresolved", "This conversation is resolved, so it can no longer be replied to."));

        // An admin may speak to the client only on a case they personally own.
        // Without this, any admin could post into any client's conversation, which
        // is exactly the impersonation the escalation rules must not permit.
        if (currentUserRoles.Contains(Roles.Admin))
        {
            if (conversation.AssignedAdminId != currentUserId)
                return Result.Failure(
                    Error.Forbidden("message.notyourcase", "Take ownership of this escalation before replying to the client."));

            return Result.Success();
        }

        if (conversation.ClientUserId == currentUserId)
            return Result.Success();

        if (conversation.EscalationStatus is ConversationEscalationStatus.Assigned)
            return Result.Failure(
                Error.Conflict("message.adminowns", "An administrator is handling this conversation. Your messages would conflict, so sending is paused until it is resolved."));

        if (await IsParticipantAsync(dbContext, conversation, currentUserId, ct))
            return Result.Success();

        return Result.Failure(
            Error.Forbidden("message.forbidden", "You cannot send a message in this conversation."));
    }
}
