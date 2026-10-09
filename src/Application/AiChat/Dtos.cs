using System.ComponentModel.DataAnnotations;
using PIPDC.Application.Properties;

namespace PIPDC.Application.AiChat;

public sealed record PersistedAiMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public List<PropertyDto>? Properties { get; set; }
}

public sealed record AiChatMessageDto(
    string Role,
    string Content,
    DateTime SentAt,
    IReadOnlyList<PropertyDto>? Properties);

public sealed record AiChatSessionDto(
    int Id,
    string? Title,
    DateTime LastMessageAt,
    IReadOnlyList<AiChatMessageDto> Messages,
    ConciergeEscalationDto? Escalation = null);

public sealed record SendAiMessageRequest(
    [Required, MaxLength(2000)] string Content);

public sealed record SendAiMessageResponseDto(
    AiChatSessionDto Session,
    AiChatMessageDto AssistantMessage,
    ConciergeEscalationDto? Escalation = null);

/// <summary>
/// A row in the admin's concierge escalation queue: the case metadata plus a
/// running summary of the underlying chat session.
/// </summary>
public sealed record ConciergeEscalationDto(
    int Id,
    int AiChatSessionId,
    string ClientUserId,
    string ClientName,
    string ClientEmail,
    string Status,
    string EscalationReason,
    DateTime EscalatedAt,
    string? AssignedAdminId,
    string? AssignedAdminName,
    DateTime? AssignedAt,
    string? ResolvedByUserId,
    string? ResolvedByName,
    DateTime? ResolvedAt,
    DateTime? LastMessageAt,
    int MessageCount);

/// <summary>
/// The full view the admin gets when they open a concierge case: the escalation
/// row plus the complete transcript of the chat that preceded it.
/// </summary>
public sealed record ConciergeEscalationDetailDto(
    ConciergeEscalationDto Escalation,
    IReadOnlyList<AiChatMessageDto> Messages);