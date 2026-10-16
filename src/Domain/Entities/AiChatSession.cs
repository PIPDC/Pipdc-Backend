using PIPDC.Domain.Common;

namespace PIPDC.Domain.Entities;

public class AiChatSession : BaseEntity
{
    public string? Title { get; set; }
    public string MessagesJson { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Set when the concierge wants to hand this chat over but must first ask the
    /// client how the team may contact them. It is written in the same turn the
    /// escalation is decided, and cleared the moment the client answers (or says
    /// something else), so the chat never escalates before the client has chosen
    /// a contact channel.
    /// </summary>
    public string? PendingEscalationReason { get; set; }

    public AppUser User { get; set; } = null!;
}
