namespace PIPDC.Domain.Enums;

/// <summary>
/// Lifecycle of an application to become a PIPDC agent.
/// Approval and license generation are implemented in a later batch; this batch
/// establishes only the authenticated submission boundary and the stored state.
/// </summary>
public enum AgentApplicationStatus
{
    Submitted = 0,
    UnderReview = 1,
    Approved = 2,
    Rejected = 3
}
