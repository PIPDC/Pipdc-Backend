namespace PIPDC.Domain.Enums;

/// <summary>
/// Lifecycle of a concierge chat that was handed to an administrator because the
/// AI assistant could not resolve the request itself.
/// <para>
/// Unlike <see cref="ConversationEscalationStatus"/> there is no <c>Active</c>
/// state here: a row only exists once the concierge has actually escalated, so an
/// open case is either waiting for an administrator (<see cref="Escalated"/>) or
/// owned by one (<see cref="Assigned"/>). One session can escalate more than once
/// over its life (e.g. a second issue after the first was resolved), so each
/// escalation is its own row.
/// </para>
/// </summary>
public enum ConciergeEscalationStatus
{
    /// <summary>
    /// The concierge handed the chat to PIPDC. No administrator has taken it yet.
    /// </summary>
    Escalated = 0,

    /// <summary>
    /// An administrator owns the case and is handling the client outside the chat.
    /// </summary>
    Assigned = 1,

    /// <summary>
    /// Closed. The row and the chat transcript remain readable as history.
    /// </summary>
    Resolved = 2
}