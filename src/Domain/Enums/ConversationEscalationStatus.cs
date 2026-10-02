namespace PIPDC.Domain.Enums;

/// <summary>
/// Lifecycle of a client conversation once it is eligible for admin help.
/// <para>
/// A conversation starts at <see cref="Active"/>, which is ordinary client-to-agent
/// chat. It only leaves <see cref="Active"/> when the handling agent escalates it,
/// so this deliberately does not model the agent's own working states: the
/// enquiry already carries those (<c>EnquiryStatus</c>), and duplicating them here
/// would create a second, competing source of truth.
/// </para>
/// </summary>
public enum ConversationEscalationStatus
{
    /// <summary>Ordinary client-to-agent conversation. Nobody has escalated it.</summary>
    Active = 0,

    /// <summary>
    /// An agent handed the issue to PIPDC. No administrator has taken it yet, so
    /// no admin is obliged to answer and the client is still with the agent.
    /// </summary>
    Escalated = 1,

    /// <summary>
    /// An administrator owns the issue and is handling the client directly. The
    /// original agent is read-only from this point so two people cannot overwrite
    /// each other in the client's thread.
    /// </summary>
    Assigned = 2,

    /// <summary>Closed. History is retained and remains readable by everyone who could read it before.</summary>
    Resolved = 3
}
