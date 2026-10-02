using System.ComponentModel.DataAnnotations;

namespace PIPDC.Application.Agents;

public record AgentReviewDto(
    int Id,
    int AgentId,
    string AgentName,
    string ReviewerUserId,
    string ReviewerName,
    int Rating,
    string? Comment,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateAgentReviewRequest(
    [Range(1, int.MaxValue, ErrorMessage = "An agent must be selected.")]
    int AgentId,

    [Range(1, 5, ErrorMessage = "Choose a rating between 1 and 5 stars.")]
    int Rating,

    [StringLength(1000, ErrorMessage = "The comment cannot exceed 1000 characters.")]
    string? Comment);

/// <summary>Public rating aggregate, returned alongside the agent's visible reviews.</summary>
public record AgentReviewSummaryDto(
    double? AverageRating,
    int ReviewCount,
    IReadOnlyList<AgentReviewDto> Reviews);
