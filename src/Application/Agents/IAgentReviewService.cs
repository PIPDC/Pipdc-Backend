using PIPDC.Domain.Common;

namespace PIPDC.Application.Agents;

public interface IAgentReviewService
{
    /// <summary>
    /// Files or updates the caller's review of an agent. One review per reviewer
    /// per agent, enforced by a unique index, so this is an upsert rather than an
    /// insert. The reviewer is taken from the JWT subject claim.
    /// </summary>
    Task<Result<AgentReviewDto>> SubmitAsync(
        string reviewerUserId,
        CreateAgentReviewRequest request,
        CancellationToken ct);

    /// <summary>
    /// The visible review list and rating aggregate for an agent. Public: the
    /// agent must be visible, so a suspended agent's reviews are not readable.
    /// </summary>
    Task<Result<AgentReviewSummaryDto>> GetForAgentAsync(
        int agentId,
        string? currentUserId,
        CancellationToken ct);

    /// <summary>The caller's own review of an agent, so the form can be prefilled. Null when not yet reviewed.</summary>
    Task<Result<AgentReviewDto?>> GetMineAsync(int agentId, string reviewerUserId, CancellationToken ct);
}
