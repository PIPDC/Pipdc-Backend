using PIPDC.Domain.Common;
using PIPDC.Domain.Enums;

namespace PIPDC.Domain.Entities;

/// <summary>
/// An authenticated user's request to be registered as a PIPDC agent.
///
/// The applicant identity is always <see cref="UserId"/>, taken from the
/// caller's JWT by the API layer. It is never accepted from the request body,
/// so a client cannot submit an application on behalf of another account.
/// </summary>
public class AgentApplication : AuditableEntity
{
    /// <summary>Owning account. Populated server-side from the authenticated user.</summary>
    public string UserId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Date of birth, date-only. Null only for rows written before this field existed.</summary>
    public DateTime? DateOfBirth { get; set; }

    public string StateOfOrigin { get; set; } = string.Empty;
    public string LocalGovernmentArea { get; set; } = string.Empty;
    public string ResidentialAddress { get; set; } = string.Empty;

    /// <summary>
    /// National Identification Number, stored as the applicant typed it apart from
    /// surrounding whitespace.
    /// </summary>
    /// <remarks>
    /// TODO: this is a government identifier, so it warrants encryption at rest
    /// and masking in the admin list. The codebase has no data-protection
    /// convention to follow yet, so it is stored in the clear for now and only
    /// ever returned in full to the owning applicant and to admins reviewing the
    /// specific application. Introduce a protector before this table holds real
    /// applicants.
    /// </remarks>
    public string? NationalIdentityNumber { get; set; }

    /// <summary>Years of prior real-estate experience, as stated by the applicant.</summary>
    public int? YearsOfExperience { get; set; }

    public string? AgencyName { get; set; }
    public string? AdditionalNotes { get; set; }

    public AgentApplicationStatus Status { get; set; } = AgentApplicationStatus.Submitted;

    // Populated by the admin review workflow in a later batch.
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByAdminId { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Set when this application becomes <see cref="AgentApplicationStatus.Revoked"/>,
    /// that is, when the registration it granted is taken away. Kept separate from
    /// <see cref="RejectionReason"/> because a rejection happens before the person
    /// ever traded, while a revocation ends a live registration.
    /// </summary>
    public string? RevocationReason { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>The administrator who revoked the registration.</summary>
    public string? RevokedByAdminId { get; set; }

    public AppUser User { get; set; } = null!;
}
