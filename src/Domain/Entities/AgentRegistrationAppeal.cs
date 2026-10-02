using PIPDC.Domain.Common;
using PIPDC.Domain.Enums;

namespace PIPDC.Domain.Entities;

/// <summary>
/// An applicant's appeal against the revocation of their agent registration.
/// </summary>
/// <remarks>
/// <para>
/// A separate entity rather than a column on <see cref="AgentApplication"/>,
/// because an appeal is an event in its own right: it has its own author, its
/// own reason written by the person appealing, and its own outcome. Modelling it
/// on the application would mean overwriting one appeal with the next and losing
/// the history of what was contested.
/// </para>
/// <para>
/// At most one appeal may be open at a time, enforced by a partial unique index on
/// <see cref="UserId"/> while the status is Submitted or UnderReview. A refused
/// appeal does not bar a fresh one, because the underlying registration was
/// revoked for a reason the applicant may be able to answer.
/// </para>
/// </remarks>
public class AgentRegistrationAppeal : AuditableEntity
{
    /// <summary>
    /// The revoked application this appeal contests. Null if that application is
    /// later hard-deleted by an administrator; the appeal is retained so a
    /// moderation decision stays on the record.
    /// </summary>
    public int? AgentApplicationId { get; set; }

    /// <summary>The applicant who lodged the appeal. Taken from the JWT, never the body.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The applicant's own account of why the revocation was wrong.</summary>
    public string Reason { get; set; } = string.Empty;

    public AgentAppealStatus Status { get; set; } = AgentAppealStatus.Submitted;

    public DateTime? ReviewedAt { get; set; }

    /// <summary>The administrator who decided the appeal.</summary>
    public string? ReviewedByAdminId { get; set; }

    /// <summary>
    /// The administrator's explanation, shown to the applicant. Required when an
    /// appeal is refused so the outcome is never a bare rejection.
    /// </summary>
    public string? DecisionNote { get; set; }

    public AppUser User { get; set; } = null!;
    public AgentApplication? AgentApplication { get; set; }
}
