using System.ComponentModel.DataAnnotations;

namespace PIPDC.Application.Conversations;

public record ConversationClientDto(
    string UserId,
    string FullName,
    string Email);

public record ConversationAgentDto(
    int? AgentId,
    // The agent's own user id. Exposed so the client can decide whether the
    // viewer is the handling agent, instead of guessing from a display name.
    // The server still authorizes the escalation, so this is a UI affordance
    // only, never a permission.
    string? UserId,
    string FullName,
    string AgencyName,
    string? PhotoUrl);

public record ConversationPropertyDto(
    int PropertyId,
    string Title,
    string Slug);

public record ConversationDto(
    int Id,
    int EnquiryId,
    ConversationClientDto Client,
    ConversationAgentDto Agent,
    ConversationPropertyDto Property,
    DateTime? LastMessageAt,
    int MessageCount,
    int UnreadCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // Escalation state. Null on the wire only if an older server omitted it; the
    // frontend treats a missing value as Active.
    string? EscalationStatus = null,
    string? EscalationReason = null,
    DateTime? EscalatedAt = null,
    string? EscalatedByName = null,
    string? AssignedAdminId = null,
    string? AssignedAdminName = null,
    DateTime? AssignedAt = null,
    DateTime? ResolvedAt = null,
    string? ResolvedByName = null);

/// <summary>Reason an agent is handing a conversation to PIPDC. Required so the admin knows what happened.</summary>
public record EscalateConversationRequest(
    [Required, MaxLength(1000), MinLength(10)] string Reason);

public record MessageDto(
    int Id,
    int ConversationId,
    string SenderUserId,
    string SenderName,
    string Content,
    DateTime CreatedAt,
    DateTime? ReadAt,
    bool IsRead);

public record SendMessageRequest(
    [Required, MaxLength(4000)] string Content);

public record EnquiryConversationStateDto(
    int EnquiryId,
    ConversationDto? Conversation,
    ConversationClientDto Client,
    ConversationAgentDto Agent,
    ConversationPropertyDto Property);

public record FirstMessageResultDto(
    ConversationDto Conversation,
    MessageDto Message);
