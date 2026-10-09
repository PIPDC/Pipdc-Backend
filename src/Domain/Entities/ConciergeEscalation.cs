using PIPDC.Domain.Common;
using PIPDC.Domain.Enums;

namespace PIPDC.Domain.Entities;

/// <summary>
/// A concierge chat handed to an administrator because the AI assistant could not
/// resolve the request itself: no property matched what the user asked for, the
/// user asked to speak to a human, or the user had an organizational request that
/// a real PIPDC person must act on.
/// <para>
/// The case hangs off the per-user <see cref="AiChatSession"/> transcript, so the
/// full conversation is already available to the queue without copying history.
/// Compared with conversation escalations, one chat can legitimately escalate
/// several times over its life, so each escalation is a separate row; there is at
/// most one open (Escalated or Assigned) row per session at any time.
/// </para>
/// </summary>
public class ConciergeEscalation : AuditableEntity
{
    public int AiChatSessionId { get; set; }

    /// <summary>Why the concierge could not help. Shown to the administrator; required.</summary>
    public string EscalationReason { get; set; } = string.Empty;

    public ConciergeEscalationStatus EscalationStatus { get; set; } = ConciergeEscalationStatus.Escalated;

    /// <summary>UTC timestamp when the concierge handed the chat to PIPDC.</summary>
    public DateTime EscalatedAt { get; set; }

    /// <summary>The administrator who currently owns the case. Null until one claims it.</summary>
    public string? AssignedAdminId { get; set; }

    public DateTime? AssignedAt { get; set; }

    /// <summary>The administrator who closed the case.</summary>
    public string? ResolvedByUserId { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public AiChatSession AiChatSession { get; set; } = null!;
    public AppUser? AssignedAdmin { get; set; }
    public AppUser? ResolvedByUser { get; set; }
}