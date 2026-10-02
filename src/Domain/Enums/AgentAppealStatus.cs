namespace PIPDC.Domain.Enums;

/// <summary>
/// An appeal a removed agent lodged against the revocation of their registration.
/// </summary>
/// <remarks>
/// An appeal is only meaningful against a <see cref="AgentApplicationStatus.Revoked"/>
/// application, because that is the one state where the platform previously
/// promised the applicant something and then took it away.
/// </remarks>
public enum AgentAppealStatus
{
    /// <summary>Lodged and waiting for an administrator to look at it.</summary>
    Submitted = 0,

    /// <summary>Claimed by an administrator and being decided.</summary>
    UnderReview = 1,

    /// <summary>
    /// Refused. The revocation stands. The applicant may submit a fresh
    /// application unless they are separately blocked from doing so.
    /// </summary>
    Refused = 2,

    /// <summary>
    /// Upheld. The registration is reinstated: the same Agent row and the same
    /// licence are restored, and the application returns to Approved.
    /// </summary>
    Upheld = 3
}
