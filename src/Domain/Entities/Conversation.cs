using PIPDC.Domain.Common;
using PIPDC.Domain.Enums;

namespace PIPDC.Domain.Entities;

public class Conversation : AuditableEntity
{
    public int EnquiryId { get; set; }
    public string ClientUserId { get; set; } = string.Empty;
    public int AgentId { get; set; }

    // UTC timestamp of the most recent message sent in this conversation.
    // Null until the first message is sent; used to order conversation lists.
    public DateTime? LastMessageAt { get; set; }

    /// <summary>
    /// Escalation lifecycle. Defaults to <see cref="ConversationEscalationStatus.Active"/>
    /// so every conversation that already exists behaves exactly as it did before
    /// escalation existed.
    /// </summary>
    public ConversationEscalationStatus EscalationStatus { get; set; } = ConversationEscalationStatus.Active;

    /// <summary>The agent who handed this conversation to PIPDC. Never the client's own id.</summary>
    public string? EscalatedByUserId { get; set; }

    /// <summary>UTC timestamp of the escalation.</summary>
    public DateTime? EscalatedAt { get; set; }

    /// <summary>Why the agent escalated. Shown to the client and the admin; required.</summary>
    public string? EscalationReason { get; set; }

    /// <summary>The administrator who currently owns the issue. Null until one claims it.</summary>
    public string? AssignedAdminId { get; set; }

    public DateTime? AssignedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    /// <summary>The administrator who closed the issue.</summary>
    public string? ResolvedByUserId { get; set; }

    public Enquiry Enquiry { get; set; } = null!;
    public AppUser Client { get; set; } = null!;
    public Agent Agent { get; set; } = null!;
    public ICollection<Message> Messages { get; set; } = [];

    public AppUser? EscalatedByUser { get; set; }
    public AppUser? AssignedAdmin { get; set; }
    public AppUser? ResolvedByUser { get; set; }
}
