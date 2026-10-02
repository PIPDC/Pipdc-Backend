namespace PIPDC.Domain.Enums;

/// <summary>
/// Moderation lifecycle of a client report against an agent.
///
/// Deliberately small: a report is either awaiting triage, being looked at, or
/// closed. <see cref="Resolved"/> means action was taken; <see cref="Dismissed"/>
/// means the report was reviewed and found not to be actionable. Both carry an
/// administrative note so the decision is auditable.
/// </summary>
public enum AgentReportStatus
{
    /// <summary>Submitted and not yet triaged. The default a new report is created with.</summary>
    Open = 0,

    /// <summary>An administrator has claimed the report and is investigating.</summary>
    UnderReview = 1,

    /// <summary>Closed after action was taken, for example suspending the agent.</summary>
    Resolved = 2,

    /// <summary>Closed without action because the report was invalid or not actionable.</summary>
    Dismissed = 3
}
