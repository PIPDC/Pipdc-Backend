namespace PIPDC.Domain.Enums;

/// <summary>
/// Lifecycle of an application to become a PIPDC agent.
///
/// <para>
/// The first four states describe the review of the application itself.
/// <see cref="Revoked"/> is different in kind: it means the application was
/// approved, the agent traded under it, and an administrator later revoked that
/// registration. It exists so the applicant's own view of their application can
/// tell the truth. Without it a removed agent's application page would keep
/// claiming "you have been approved as an agent" while the person could no
/// longer reach a single agent screen.
/// </para>
/// </summary>
public enum AgentApplicationStatus
{
    Submitted = 0,
    UnderReview = 1,
    Approved = 2,
    Rejected = 3,

    /// <summary>
    /// The registration granted by this application was revoked by an
    /// administrator. The applicant may appeal, and may submit a fresh
    /// application if the appeal is refused.
    /// </summary>
    Revoked = 4
}
