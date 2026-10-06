using PIPDC.Domain.Entities;

namespace PIPDC.Application.Agents;

public static class AgentMappers
{
    public static AgentDto ToDto(this Agent agent, int propertyCount, double? averageRating, int reviewCount) =>
        new(
            agent.Id,
            agent.Bio,
            agent.Title,
            agent.PhotoUrl,
            agent.PhotoPublicId,
            agent.AgencyName,
            agent.LicenseNumber,
            agent.PhoneNumber,
            agent.IsVerified,
            agent.User.FullName,
            agent.UserId,
            agent.User.Email!,
            agent.User.FirstName,
            agent.User.LastName,
            agent.CreatedAt,
            agent.UpdatedAt,
            propertyCount,
            averageRating,
            reviewCount,
            agent.IsSuspended,
            agent.SuspendedAt,
            agent.SuspensionReason,
            agent.IsRemoved,
            agent.RemovedAt,
            agent.RemovalReason,
            agent.ReassignedToAgentId);

    /// <summary>
    /// Maps a report for the moderation queue. The reporter's email is included
    /// because an administrator triaging a report needs a way to contact them.
    ///
    /// Requires Agent.User and Reporter to be loaded, so callers must materialize
    /// the entity and map afterwards. The null-forgiving names are guarded with
    /// fallbacks so a missing navigation degrades to a placeholder rather than
    /// taking down the whole queue with a NullReferenceException.
    /// </summary>
    public static AgentReportDto ToDto(this AgentReport report) =>
        new(
            report.Id,
            report.AgentId,
            report.Agent?.User?.FullName ?? "Unknown agent",
            report.Agent?.AgencyName ?? string.Empty,
            report.ReporterUserId,
            report.Reporter?.FullName ?? "Unknown reporter",
            report.Reporter?.Email ?? string.Empty,
            report.Reason,
            report.Status,
            report.Description,
            report.CreatedAt,
            report.UpdatedAt,
            report.ReviewedAt,
            report.ReviewedByAdminId,
            report.ResolutionNote);

    /// <summary>
    /// Maps a review for the public agent profile. Requires Agent.User and
    /// Reviewer to be loaded; see the note on the report mapper.
    /// </summary>
    public static AgentReviewDto ToDto(this AgentReview review) =>
        new(
            review.Id,
            review.AgentId,
            review.Agent?.User?.FullName ?? "Unknown agent",
            review.ReviewerUserId,
            review.Reviewer?.FullName ?? "Anonymous",
            review.Rating,
            review.Comment,
            review.CreatedAt,
            review.UpdatedAt);
}
