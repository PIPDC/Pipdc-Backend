using Microsoft.EntityFrameworkCore;
using PIPDC.Application.Common;
using PIPDC.Application.Data;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;

namespace PIPDC.Application.Agents;

public class AgentReviewService(IAppDbContext dbContext) : IAgentReviewService
{
    // Same shape as the guard in AgentApplicationService, so a genuine database
    // fault is never swallowed as a handled race.
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;

    public async Task<Result<AgentReviewDto>> SubmitAsync(
        string reviewerUserId,
        CreateAgentReviewRequest request,
        CancellationToken ct)
    {
        // The rating is a whole number of stars. The check-constraint on the column
        // is a backstop; this gives a readable message and keeps the range explicit.
        if (request.Rating is < 1 or > 5)
            return Result<AgentReviewDto>.Failure(
                Error.Validation("review.invalidrating", "Choose a rating between 1 and 5 stars."));

        var comment = request.Comment?.Trim();
        if (string.IsNullOrEmpty(comment))
            comment = null;
        else if (comment.Length > 1000)
            return Result<AgentReviewDto>.Failure(
                Error.Validation("review.commenttoolong", "The comment cannot exceed 1000 characters."));

        var agent = await dbContext.Agents
            .VisibleAgents()
            .FirstOrDefaultAsync(a => a.Id == request.AgentId, ct);

        if (agent is null)
            return Result<AgentReviewDto>.Failure(
                Error.NotFound("review.agentnotfound", $"Agent with id {request.AgentId} was not found."));

        // Reviews are a client signal about an agent, so an agent reviewing
        // itself would only ever inflate its own average.
        var reviewerAgentId = await dbContext.Agents
            .Where(a => a.UserId == reviewerUserId)
            .Select(a => (int?)a.Id)
            .FirstOrDefaultAsync(ct);

        if (reviewerAgentId == agent.Id)
            return Result<AgentReviewDto>.Failure(
                Error.Validation("review.selfreview", "You cannot review your own agent profile."));

        // Upsert on (AgentId, ReviewerUserId). One client can change their mind
        // about a rating, but cannot stack a second vote on the same agent.
        var existing = await dbContext.AgentReviews
            .Include(r => r.Agent)
                .ThenInclude(a => a.User)
            .Include(r => r.Reviewer)
            .FirstOrDefaultAsync(r => r.AgentId == agent.Id && r.ReviewerUserId == reviewerUserId, ct);

        if (existing is null)
        {
            existing = new AgentReview
            {
                AgentId = agent.Id,
                ReviewerUserId = reviewerUserId,
                Rating = request.Rating,
                Comment = comment,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.AgentReviews.Add(existing);
        }
        else
        {
            existing.Rating = request.Rating;
            existing.Comment = comment;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<AgentReviewDto>.Failure(Error.Concurrency());
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Two concurrent first submissions for the same agent and reviewer
            // both see no existing row, and the unique index rejects the second
            // insert. The caller's intent is already stored by the winning
            // request, so report success rather than a server error. The failed
            // entity is detached first so it is not retried on a later save.
            dbContext.AgentReviews.Entry(existing).State = EntityState.Detached;

            var winner = await dbContext.AgentReviews
                .AsNoTracking()
                .Include(r => r.Agent)
                    .ThenInclude(a => a.User)
                .Include(r => r.Reviewer)
                .FirstOrDefaultAsync(r => r.AgentId == agent.Id && r.ReviewerUserId == reviewerUserId, ct);

            return winner is null
                ? Result<AgentReviewDto>.Failure(
                    Error.Failure("review.savefailed", "The review could not be saved."))
                : Result<AgentReviewDto>.Success(winner.ToDto());
        }

        var saved = await dbContext.AgentReviews
            .AsNoTracking()
            .Include(r => r.Agent)
                .ThenInclude(a => a.User)
            .Include(r => r.Reviewer)
            .FirstOrDefaultAsync(r => r.Id == existing.Id, ct);

        if (saved is null)
            return Result<AgentReviewDto>.Failure(
                Error.Failure("review.savefailed", "The review could not be read back after saving."));

        return Result<AgentReviewDto>.Success(saved.ToDto());
    }

    public async Task<Result<AgentReviewSummaryDto>> GetForAgentAsync(
        int agentId,
        string? currentUserId,
        CancellationToken ct)
    {
        // Reviews are public content about a public agent. Reading them for a
        // suspended agent would confirm the agent exists.
        if (!await dbContext.Agents.VisibleAgents().AnyAsync(a => a.Id == agentId, ct))
            return Result<AgentReviewSummaryDto>.Failure(
                Error.NotFound("review.agentnotfound", $"Agent with id {agentId} was not found."));

        // The reviews are materialized before mapping because ToDto reads
        // Reviewer and Agent.User. Projecting inside Select() runs in the
        // database, where those navigations are unavailable, so the mapping
        // threw a NullReferenceException and the panel always rendered empty.
        var entities = await dbContext.AgentReviews
            .AsNoTracking()
            .Include(r => r.Agent)
                .ThenInclude(a => a.User)
            .Include(r => r.Reviewer)
            .Where(r => r.AgentId == agentId)
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .ToListAsync(ct);

        var reviews = entities.Select(r => r.ToDto()).ToList();

        // Average in memory from the page we already loaded: the review list is
        // capped, so this is the average of what the caller can see, not of the
        // full table. The authoritative average lives on the agent projection,
        // which is computed in the database over all reviews.
        double? average = reviews.Count == 0
            ? null
            : Math.Round(reviews.Average(r => (double)r.Rating), 2);

        return Result<AgentReviewSummaryDto>.Success(
            new AgentReviewSummaryDto(average, reviews.Count, reviews));
    }

    public async Task<Result<AgentReviewDto?>> GetMineAsync(
        int agentId,
        string reviewerUserId,
        CancellationToken ct)
    {
        if (!await dbContext.Agents.AnyAsync(a => a.Id == agentId, ct))
            return Result<AgentReviewDto?>.Failure(
                Error.NotFound("review.agentnotfound", $"Agent with id {agentId} was not found."));

        // Materialized before mapping, for the same navigation reason as
        // GetForAgentAsync.
        var mineEntity = await dbContext.AgentReviews
            .AsNoTracking()
            .Include(r => r.Agent)
                .ThenInclude(a => a.User)
            .Include(r => r.Reviewer)
            .Where(r => r.AgentId == agentId && r.ReviewerUserId == reviewerUserId)
            .FirstOrDefaultAsync(ct);

        return Result<AgentReviewDto?>.Success(mineEntity?.ToDto());
    }
}
