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
    public string StateOfOrigin { get; set; } = string.Empty;
    public string ResidentialAddress { get; set; } = string.Empty;
    public string LocalGovernmentArea { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    public string? AgencyName { get; set; }
    public string? AdditionalNotes { get; set; }

    public AgentApplicationStatus Status { get; set; } = AgentApplicationStatus.Submitted;

    // Populated by the admin review workflow in a later batch.
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByAdminId { get; set; }
    public string? RejectionReason { get; set; }

    public AppUser User { get; set; } = null!;
}
