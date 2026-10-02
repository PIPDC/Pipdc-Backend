using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PIPDC.Application.Conversations;
using PIPDC.Application.Data;

namespace PIPDC.API.Hubs;

// Phase 2 SignalR — Step 2: authenticated identity + conversation groups.
//
// The hub contains no persistence, no conversation/message creation, and no
// business logic. Authorization reuses the application's authoritative
// ConversationAuthorization rules. REST remains the persistence mechanism.
//
// The authenticated identity comes from Context.User (the JWT-backed
// ClaimsPrincipal): UserIdentifier is the "sub" claim via
// JwtSubUserIdProvider, and roles are the "role" claims. The client never
// supplies its own identity, ownership information, or group names.
[Authorize]
public class MessagingHub(IAppDbContext dbContext) : Hub
{
    // Server-to-client event for a successfully persisted message. Payload is
    // the existing MessageDto; event payloads are never accepted from clients.
    public const string NewMessageEvent = "NewMessage";

    // Server-to-client event for an escalation state change (escalated, claimed,
    // or resolved). Payload is the existing ConversationDto, so a connected
    // client updates its banner and composer from the same shape it already
    // renders. Same hub and same conversation group as NewMessage: escalation is
    // a change to a conversation, not a separate messaging channel.
    public const string ConversationEscalationChangedEvent = "ConversationEscalationChanged";

    // Join the group conversation:{conversationId}. Joining a group is not
    // itself authorization: the authenticated user is checked against the
    // existing ConversationAuthorization rules before the connection is added.
    public async Task JoinConversation(int conversationId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
            throw new HubException("conversation.unauthenticated");

        var roles = Context.User?.FindAll("role").Select(c => c.Value).ToList() ?? [];

        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, Context.ConnectionAborted);

        if (conversation is null)
            throw new HubException("conversation.notfound");

        if (!await ConversationAuthorization.CanAccessConversationAsync(
                dbContext, conversation, userId, roles, Context.ConnectionAborted))
        {
            throw new HubException("conversation.forbidden");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ConversationGroup.For(conversationId), Context.ConnectionAborted);
    }

    // Remove the connection from conversation:{conversationId}. Leaving a group
    // only removes this connection; it never touches PostgreSQL.
    public Task LeaveConversation(int conversationId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, ConversationGroup.For(conversationId), Context.ConnectionAborted);
    }
}
