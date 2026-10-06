namespace PIPDC.Application.Conversations;

// Realtime fan-out for conversation changes.
//
// The Application layer owns the decision of WHAT changed and WHEN; the API
// layer owns HOW it reaches the browser. Keeping that boundary here is what
// stops application services from taking a dependency on the transport, the
// hub type, the group-name convention or the event-name constants - all of
// which belong to the delivery mechanism and not to the business rule.
//
// Implementations are best-effort: callers are expected to have already
// committed their write, and a failed notification must never turn a successful
// persistence into a failed request.
public interface IMessageNotifier
{
    /// Broadcast a freshly persisted message to its conversation's group.
    Task NotifyNewMessageAsync(int conversationId, MessageDto message, CancellationToken cancellationToken);

    /// Broadcast an escalation state change (escalated, claimed or resolved)
    /// to the same conversation's group.
    Task NotifyEscalationChangedAsync(ConversationDto conversation, CancellationToken cancellationToken);
}
