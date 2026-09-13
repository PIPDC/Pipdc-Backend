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
    IReadOnlyList<AiChatMessageDto> Messages);

public sealed record SendAiMessageRequest(
    [Required, MaxLength(2000)] string Content);

public sealed record SendAiMessageResponseDto(
    AiChatSessionDto Session,
    AiChatMessageDto AssistantMessage);