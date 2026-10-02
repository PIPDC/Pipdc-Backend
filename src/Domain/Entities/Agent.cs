using PIPDC.Domain.Common;

namespace PIPDC.Domain.Entities;

public class Agent : AuditableEntity
{
    public string? Bio { get; set; }
    public string? Title { get; set; }
    public string? PhotoUrl { get; set; }
    public string? PhotoPublicId { get; set; }
    public string AgencyName { get; set; } = string.Empty;
    public string? LicenseNumber { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsVerified { get; set; }

    // Moderation state. This is an AGENT-level concern and is deliberately kept
    // separate from Property.Status, which continues to describe the property
    // itself (available / pending / sold / rented / unavailable). An agent being
    // suspended must never rewrite the status of the properties they own.
    public bool IsSuspended { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public string? SuspensionReason { get; set; }

    /// <summary>
    /// The administrator who suspended this agent, so a moderation decision is
    /// attributable. Set from the JWT subject claim and cleared on reinstatement.
    /// </summary>
    public string? SuspendedByAdminId { get; set; }

    // Removal is a stronger decision than suspension. A suspended agent is still
    // an agent who is temporarily unable to trade; a removed agent's registration
    // has been revoked, so the Agent role is dropped and they fall back to a plain
    // user account.
    //
    // The row is deliberately retained rather than deleted. Properties, enquiries,
    // conversations, reports and reviews all point at this agent, and hard-deleting
    // either destroys that history or cascades it away. Keeping the row also makes
    // an approved appeal a genuine reinstatement of the same registration, with the
    // same licence, rather than the creation of a new one.
    public bool IsRemoved { get; set; }
    public DateTime? RemovedAt { get; set; }
    public string? RemovalReason { get; set; }

    /// <summary>
    /// The agent whose listings and enquiries inherited this agent's work when the
    /// registration was revoked. Null when the admin chose not to reassign, in
    /// which case the listings stay with the removed agent and remain hidden from
    /// the public site.
    /// </summary>
    public int? ReassignedToAgentId { get; set; }

    /// <summary>The administrator who revoked this registration, for attribution.</summary>
    public string? RemovedByAdminId { get; set; }

    /// <summary>
    /// Set when the removed agent appealed and the appeal was upheld, so the
    /// outcome of the appeal is visible from the agent record itself.
    /// </summary>
    public DateTime? ReinstatedAt { get; set; }

    public string UserId { get; set; } = string.Empty;

    public AppUser User { get; set; } = null!;
    public AppUser? SuspendedByAdmin { get; set; }
    public AppUser? RemovedByAdmin { get; set; }

    /// <summary>The agent that inherited this agent's listings and enquiries on removal.</summary>
    public Agent? ReassignedToAgent { get; set; }
    public ICollection<Property> Properties { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<AgentReport> Reports { get; set; } = [];
    public ICollection<AgentReview> Reviews { get; set; } = [];
}
