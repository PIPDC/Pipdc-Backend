using Microsoft.AspNetCore.SignalR;
using PIPDC.Application.Conversations;

namespace PIPDC.API.Hubs;

// The only type in the build that knows about hubs, group names and event
// names. Application services ask for IMessageNotifier and stay ignorant of
// SignalR entirely, which is what makes the layering enforceable by the
// compiler rather than by review.
//
// Nothing here decides whether a notification should be sent - the callers
// have already committed their write and own the best-effort catch. This class
// only performs the fan-out, using the same group and the same event name the
// hub has always used.
public class SignalRMessageNotifier(IHubContext<MessagingHub> hubContext) : IMessageNotifier
{
    public Task NotifyNewMessageAsync(int conversationId, MessageDto message, CancellationToken cancellationToken)
        => hubContext.Clients
            .Group(ConversationGroup.For(conversationId))
            .SendAsync(MessagingHub.NewMessageEvent, message, cancellationToken);

    public Task NotifyEscalationChangedAsync(ConversationDto conversation, CancellationToken cancellationToken)
        => hubContext.Clients
            .Group(ConversationGroup.For(conversation.Id))
            .SendAsync(MessagingHub.ConversationEscalationChangedEvent, conversation, cancellationToken);
}
