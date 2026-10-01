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

    public string UserId { get; set; } = string.Empty;

    public AppUser User { get; set; } = null!;
    public ICollection<Property> Properties { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
}
