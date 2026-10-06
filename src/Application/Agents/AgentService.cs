using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PIPDC.Application.Auth;
using PIPDC.Application.Common;
using PIPDC.Application.Data;
using PIPDC.Application.Email;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Agents;

public class AgentService(
    IAppDbContext dbContext,
    UserManager<AppUser> userManager,
    IEmailQueue emailQueue,
    IOptions<GmailApiSettings> gmailOptions,
    ILogger<AgentService> logger) : IAgentService
{
    public async Task<Result<PaginatedResult<AgentDto>>> GetAllAsync(
        AgentQueryParameters q,
        bool includeModerated,
        CancellationToken ct)
    {
        // The public directory hides revoked, suspended and unverified agents.
        // includeModerated is passed by the API layer only when the caller both
        // holds the Admin role and asked for it, so a public caller can neither
        // opt themselves into moderated agents nor have them leaked to them by
        // holding a role.
        IQueryable<Agent> query = includeModerated
            ? dbContext.Agents
            : dbContext.Agents.VisibleAgents();

        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var keyword = q.Keyword.ToLower();
            query = query.Where(a => a.AgencyName.ToLower().Contains(keyword));
        }

        if (q.IsVerified.HasValue)
            query = query.Where(a => a.IsVerified == q.IsVerified.Value);

        var totalCount = await query.CountAsync(ct);

        query = q.SortBy?.ToLower() switch
        {
            "agencyname" => q.SortDescending ? query.OrderByDescending(a => a.AgencyName)
                                             : query.OrderBy(a => a.AgencyName),
            _ => q.SortDescending ? query.OrderByDescending(a => a.CreatedAt)
                                  : query.OrderBy(a => a.CreatedAt)
        };

        var items = await query
            .Skip((q.EffectivePageNumber - 1) * q.PageSize)
            .Take(q.PageSize)
            .Select(a => new AgentDto(
                a.Id,
                a.Bio,
                a.Title,
                a.PhotoUrl,
                a.PhotoPublicId,
                a.AgencyName,
                a.LicenseNumber,
                a.PhoneNumber,
                a.IsVerified,
                a.User.FullName,
                a.UserId,
                a.User.Email!,
                a.User.FirstName,
                a.User.LastName,
                a.CreatedAt,
                a.UpdatedAt,
                a.Properties.Count,
                a.Reviews.Count == 0 ? null : Math.Round(a.Reviews.Average(r => r.Rating), 2),
                a.Reviews.Count,
                // Always false for a public caller, because VisibleAgents() has
                // already excluded every suspended agent from this result set.
                a.IsSuspended,
                a.SuspendedAt,
                a.SuspensionReason,
                // Likewise false publicly; the admin directory is the only caller
                // that reaches these, which is what lets it badge a revoked agent.
                a.IsRemoved,
                a.RemovedAt,
                a.RemovalReason,
                a.ReassignedToAgentId))
            .ToListAsync(ct);
        return Result<PaginatedResult<AgentDto>>.Success(
            PaginatedResult<AgentDto>.Create(items, totalCount, q.EffectivePageNumber, q.PageSize));
    }

    /// <summary>
    /// The public rating aggregate for one agent. Returns null rather than zero
    /// when there are no reviews so the client can tell "unrated" from "rated 0".
    /// </summary>
    private async Task<(double? AverageRating, int ReviewCount)> GetReviewAggregateAsync(
        int agentId,
        CancellationToken ct)
    {
        var aggregate = await dbContext.AgentReviews
            .Where(r => r.AgentId == agentId)
            .GroupBy(_ => 1)
            .Select(g => new { Average = g.Average(r => r.Rating), Count = g.Count() })
            .FirstOrDefaultAsync(ct);

        return aggregate is null || aggregate.Count == 0
            ? (null, 0)
            : (Math.Round(aggregate.Average, 2), aggregate.Count);
    }

    public async Task<Result<AgentDto>> GetByIdAsync(int id, bool includeSuspended, CancellationToken ct)
    {
        IQueryable<Agent> query = dbContext.Agents.Include(a => a.User);
        if (!includeSuspended)
            query = query.VisibleAgents();

        var agent = await query.FirstOrDefaultAsync(a => a.Id == id, ct);

        if (agent is null)
            return Result<AgentDto>.Failure(
                Error.NotFound("agent.notfound", $"Agent with id {id} was not found."));

        var propertyCount = await dbContext.Properties
            .VisibleProperties()
            .CountAsync(p => p.AgentId == id, ct);

        var (averageRating, reviewCount) = await GetReviewAggregateAsync(id, ct);

        return Result<AgentDto>.Success(agent.ToDto(propertyCount, averageRating, reviewCount));
    }

    public async Task<Result<AgentDto>> GetMyProfileAsync(string userId, CancellationToken ct)
    {
        var agent = await dbContext.Agents
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.UserId == userId, ct);

        if (agent is null)
            return Result<AgentDto>.Failure(
                Error.NotFound("agent.notfound", "You do not have an agent profile."));

        var propertyCount = await dbContext.Properties.CountAsync(p => p.AgentId == agent.Id, ct);
        var (averageRating, reviewCount) = await GetReviewAggregateAsync(agent.Id, ct);

        return Result<AgentDto>.Success(agent.ToDto(propertyCount, averageRating, reviewCount));
    }

    public async Task<Result<AgentDto>> CreateAsync(CreateAgentRequest request, CancellationToken ct)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
            return Result<AgentDto>.Failure(
                Error.Conflict("agent.duplicateemail", "A user with this email already exists."));

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            return Result<AgentDto>.Failure(
                Error.Validation("agent.identityfailed", errors));
        }

        await userManager.AddToRoleAsync(user, Roles.Agent);

        var agent = new Agent
        {
            Title = request.Title,
            PhotoUrl = request.PhotoUrl,
            PhotoPublicId = request.PhotoPublicId,
            Bio = request.Bio,
            AgencyName = request.AgencyName,
            LicenseNumber = request.LicenseNumber,
            PhoneNumber = request.PhoneNumber,
            IsVerified = false,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Agents.Add(agent);
        await dbContext.SaveChangesAsync(ct);

        var created = await dbContext.Agents
            .Include(a => a.User)
            .FirstAsync(a => a.Id == agent.Id, ct);

        return Result<AgentDto>.Success(created.ToDto(0, null, 0));
    }

    public async Task<Result<AgentDto>> UpdateAsync(int id, UpdateAgentRequest request, CancellationToken ct)
    {
        var agent = await dbContext.Agents
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (agent is null)
            return Result<AgentDto>.Failure(
                Error.NotFound("agent.notfound", $"Agent with id {id} was not found."));

        agent.Title = request.Title;
        agent.PhotoUrl = request.PhotoUrl;
        agent.PhotoPublicId = request.PhotoPublicId;
        agent.Bio = request.Bio;
        agent.AgencyName = request.AgencyName;
        // A blank license field must clear the column rather than store an empty
        // string. The unique index is filtered on "IS NOT NULL", so an empty
        // string would pass the check on its own but collide with any other
        // agent that also had a blank license, surfacing as a 500.
        agent.LicenseNumber = string.IsNullOrWhiteSpace(request.LicenseNumber)
            ? null
            : request.LicenseNumber.Trim();
        agent.PhoneNumber = request.PhoneNumber;
        agent.IsVerified = request.IsVerified;
        agent.UpdatedAt = DateTime.UtcNow;

        // Surface a duplicate license as a conflict the admin can act on,
        // instead of the unique-index violation escaping as a 500.
        if (agent.LicenseNumber is not null)
        {
            var licenseTaken = await dbContext.Agents
                .AnyAsync(a => a.Id != id && a.LicenseNumber == agent.LicenseNumber, ct);

            if (licenseTaken)
                return Result<AgentDto>.Failure(
                    Error.Conflict("agent.duplicatelicense", "Another agent already uses this license number."));
        }

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<AgentDto>.Failure(Error.Concurrency());
        }

        var propertyCount = await dbContext.Properties.CountAsync(p => p.AgentId == id, ct);
        var (averageRating, reviewCount) = await GetReviewAggregateAsync(id, ct);

        return Result<AgentDto>.Success(agent.ToDto(propertyCount, averageRating, reviewCount));
    }

    /// <summary>
    /// Revokes an agent registration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaces a hard delete. Deleting the Agent row left the platform in the
    /// worst possible state: the person's properties and enquiries still pointed at
    /// them, their application still read "Approved", and the only signal to the user
    /// was that agent screens started rejecting them. Nothing was ever sent to
    /// explain it, and there was no route back.
    /// </para>
    /// <para>
    /// The row is retained and marked removed, and four things happen together:
    /// the Agent role is dropped so the agent dashboard actually stops being
    /// reachable, the application that granted the registration moves to Revoked so
    /// the applicant's own page tells the truth, the agent is emailed, and any
    /// listings and enquiries can be handed to another agent so clients are not
    /// stranded.
    /// </para>
    /// </remarks>
    public async Task<Result<AgentRemovalResult>> RemoveAsync(
        int id,
        string adminUserId,
        RemoveAgentRequest? request,
        CancellationToken ct)
    {
        var reason = request?.Reason?.Trim() ?? string.Empty;

        if (reason.Length is < 10 or > 1000)
            return Result<AgentRemovalResult>.Failure(Error.Validation(
                "agent.invalidremovalreason",
                "The removal reason must be between 10 and 1000 characters. The agent is emailed this, so it has to say why."));

        var agent = await dbContext.Agents
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (agent is null)
            return Result<AgentRemovalResult>.Failure(
                Error.NotFound("agent.notfound", $"Agent with id {id} was not found."));

        // Idempotent, like suspension: a repeated removal keeps the first reason and
        // decision-maker so a second click cannot overwrite the record of why.
        //
        // It still drops the role. An earlier attempt can commit the row change and
        // then fail to revoke the Identity role, and returning early here would make
        // that state permanent: the agent stays able to open the agent dashboard
        // while the admin has nothing left to click.
        if (agent.IsRemoved)
        {
            var roleRevoked = await RevokeAgentRoleAsync(agent, id, ct);

            return roleRevoked.IsFailure
                ? Result<AgentRemovalResult>.Failure(roleRevoked.Error)
                : Result<AgentRemovalResult>.Success(new AgentRemovalResult(
                    agent.Id,
                    0,
                    0,
                    agent.ReassignedToAgentId is not null,
                    false,
                    "This agent was already removed."));
        }

        // The replacement must be a real, trading agent. Reassigning to a suspended
        // or removed agent would silently move the work somewhere equally dead.
        Agent? successor = null;
        if (request?.ReassignToAgentId is { } successorId)
        {
            if (successorId == agent.Id)
                return Result<AgentRemovalResult>.Failure(Error.Validation(
                    "agent.invalidsuccessor",
                    "An agent's listings cannot be reassigned to that same agent."));

            successor = await dbContext.Agents
                .FirstOrDefaultAsync(a => a.Id == successorId, ct);

            if (successor is null)
                return Result<AgentRemovalResult>.Failure(Error.NotFound(
                    "agent.successornotfound",
                    $"The agent chosen to take over these listings (id {successorId}) was not found."));

            if (successor.IsRemoved || successor.IsSuspended)
                return Result<AgentRemovalResult>.Failure(Error.Validation(
                    "agent.successorunavailable",
                    "Listings can only be reassigned to an agent who is active and not suspended."));
        }

        agent.IsRemoved = true;
        agent.RemovedAt = DateTime.UtcNow;
        agent.RemovalReason = reason;
        agent.RemovedByAdminId = adminUserId;
        agent.ReassignedToAgentId = successor?.Id;
        agent.UpdatedAt = DateTime.UtcNow;

        // The application that granted this registration must stop claiming the
        // person is an approved agent. Without this the applicant's own page keeps
        // showing "you have been approved" while every agent screen refuses them.
        var approvedApplication = await dbContext.AgentApplications
            .Where(a => a.UserId == agent.UserId && a.Status == AgentApplicationStatus.Approved)
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (approvedApplication is not null)
        {
            approvedApplication.Status = AgentApplicationStatus.Revoked;
            approvedApplication.RevocationReason = reason;
            approvedApplication.RevokedAt = agent.RemovedAt;
            approvedApplication.RevokedByAdminId = adminUserId;
            approvedApplication.UpdatedAt = agent.RemovedAt;
        }

        var propertiesMoved = 0;
        var enquiriesMoved = 0;

        if (successor is not null)
        {
            var properties = await dbContext.Properties
                .Where(p => p.AgentId == agent.Id)
                .ToListAsync(ct);

            foreach (var property in properties)
            {
                property.AgentId = successor.Id;
                property.UpdatedAt = DateTime.UtcNow;
            }

            propertiesMoved = properties.Count;

            // Open enquiries only, and reached through the property, because an
            // Enquiry is owned by a listing rather than by an agent directly. A
            // resolved enquiry belongs to the history of the agent who had it and is
            // left alone.
            var openEnquiries = await dbContext.Enquiries
                .Where(e => e.Property.AgentId == agent.Id && e.Status != EnquiryStatus.Resolved)
                .ToListAsync(ct);

            // An enquiry has no agent column to move. Handing it over means handing
            // its conversation to the successor, because that is what routes replies
            // and notifications. Enquiries with no conversation yet are moved by
            // reassigning the underlying property above, which is already done.
            var enquiryIds = openEnquiries.Select(e => e.Id).ToList();

            if (enquiryIds.Count > 0)
            {
                var conversations = await dbContext.Conversations
                    .Where(c => enquiryIds.Contains(c.EnquiryId))
                    .ToListAsync(ct);

                foreach (var conversation in conversations)
                {
                    conversation.AgentId = successor.Id;
                    conversation.UpdatedAt = DateTime.UtcNow;
                }

                enquiriesMoved = openEnquiries.Count;
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<AgentRemovalResult>.Failure(Error.Concurrency());
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to remove agent {AgentId}", id);
            return Result<AgentRemovalResult>.Failure(
                Error.Failure("agent.removalfailed", "The agent could not be removed. Please try again."));
        }

        // The role lives in Identity, not in the entities above, so it is revoked
        // only once the data changes are safely committed. A user left holding the
        // Agent role with no agent profile is the exact broken state this whole
        // change exists to eliminate.
        var revoked = await RevokeAgentRoleAsync(agent, id, ct);
        if (revoked.IsFailure)
            return Result<AgentRemovalResult>.Failure(revoked.Error);

        var user = await userManager.FindByIdAsync(agent.UserId);

        if (user is not null && !string.IsNullOrWhiteSpace(user.Email))
        {
            // Best effort. The removal is already committed, so a full queue must
            // not make the admin's action look like it failed.
            if (emailQueue.Enqueue(EmailTemplates.AgentRegistrationRemoved(
                    user.Email,
                    agent.User?.FullName ?? user.UserName ?? "Agent",
                    reason,
                    successor is not null,
                    gmailOptions.Value.FrontendBaseUrl)) == EnqueueResult.Dropped)
            {
                logger.LogError(
                    "Email queue was full; the removal notification for agent {AgentId} was dropped.", id);
            }
        }

        return Result<AgentRemovalResult>.Success(new AgentRemovalResult(
            agent.Id,
            propertiesMoved,
            enquiriesMoved,
            successor is not null,
            approvedApplication is not null,
            $"Agent {agent.Id} was removed."));
    }

    /// <summary>
    /// Drops the Agent role from Identity and fails the operation if that does not
    /// work, rather than logging and moving on.
    /// </summary>
    /// <remarks>
    /// A row marked removed while the user keeps the Agent role is the reported bug in
    /// its exact shape: the platform says "not an agent" and the API says otherwise,
    /// and every agent screen the user opens is a 403 they cannot explain. The data
    /// change is committed before this runs, so the caller gets a partial-failure
    /// result and can re-invoke the same removal, which is why the removed-agent
    /// branch above calls this too.
    /// </remarks>
    private async Task<Result> RevokeAgentRoleAsync(Agent agent, int agentId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(agent.UserId);

        if (user is null)
        {
            logger.LogWarning("Agent {AgentId} was removed but their account could not be reloaded.", agentId);
            return Result.Failure(Error.NotFound(
                "agent.usernotfound", "This agent's account could not be reloaded, so the agent role could not be revoked."));
        }

        if (!await userManager.IsInRoleAsync(user, Roles.Agent))
            return Result.Success();

        var roleResult = await userManager.RemoveFromRoleAsync(user, Roles.Agent);

        if (!roleResult.Succeeded)
        {
            logger.LogError(
                "Agent {AgentId} was marked removed but the Agent role could not be revoked for {UserId}. "
                + "The user may still reach role-gated agent routes. Errors: {Errors}",
                agentId,
                agent.UserId,
                string.Join("; ", roleResult.Errors.Select(e => e.Description)));

            return Result.Failure(Error.Failure(
                "agent.rolerevokefailed",
                "The agent was marked removed, but their agent access could not be revoked. Please run the removal again."));
        }

        return Result.Success();
    }

    public async Task<Result<AgentDto>> ToggleVerificationAsync(int agentId, CancellationToken ct)
    {
        var agent = await dbContext.Agents
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == agentId, ct);

        if (agent is null)
            return Result<AgentDto>.Failure(
                Error.NotFound("agent.notfound", $"Agent with id {agentId} was not found."));

        agent.IsVerified = !agent.IsVerified;
        agent.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<AgentDto>.Failure(Error.Concurrency());
        }

        var propertyCount = await dbContext.Properties.CountAsync(p => p.AgentId == agentId, ct);
        var (averageRating, reviewCount) = await GetReviewAggregateAsync(agentId, ct);

        return Result<AgentDto>.Success(agent.ToDto(propertyCount, averageRating, reviewCount));
    }

    public async Task<Result<AgentSummaryDto>> GetSummaryAsync(int agentId, bool includeSuspended, CancellationToken ct)
    {
        IQueryable<Agent> query = dbContext.Agents.Include(a => a.User);
        if (!includeSuspended)
            query = query.VisibleAgents();

        var agent = await query.FirstOrDefaultAsync(a => a.Id == agentId, ct);

        if (agent is null)
            return Result<AgentSummaryDto>.Failure(
                Error.NotFound("agent.notfound", $"Agent with id {agentId} was not found."));

        var propertyCount = await dbContext.Properties
            .VisibleProperties()
            .CountAsync(p => p.AgentId == agentId, ct);

        var enquiryCount = await dbContext.Enquiries
            .CountAsync(e => e.Property.AgentId == agentId, ct);

        var conversationCount = await dbContext.Conversations
            .CountAsync(c => c.AgentId == agentId, ct);

        var (averageRating, reviewCount) = await GetReviewAggregateAsync(agentId, ct);

        // The moderation backlog is internal. This endpoint is public, so the
        // count is only computed for an admin caller; everyone else gets 0
        // rather than a signal about how many reports an agent has attracted.
        var openReportCount = includeSuspended
            ? await dbContext.AgentReports.CountAsync(r => r.AgentId == agentId
                && (r.Status == AgentReportStatus.Open || r.Status == AgentReportStatus.UnderReview), ct)
            : 0;

        return Result<AgentSummaryDto>.Success(new AgentSummaryDto(
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
            agent.CreatedAt,
            agent.UpdatedAt,
            propertyCount,
            enquiryCount,
            conversationCount,
            averageRating,
            reviewCount,
            openReportCount,
            agent.IsSuspended,
            agent.SuspendedAt,
            agent.SuspensionReason,
            agent.IsRemoved,
            agent.RemovedAt,
            agent.RemovalReason,
            agent.ReassignedToAgentId));
    }

    public async Task<Result<AgentSummaryDto>> SuspendAsync(
        int agentId,
        string adminUserId,
        SuspendAgentRequest request,
        CancellationToken ct)
    {
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 10 or > 500)
            return Result<AgentSummaryDto>.Failure(
                Error.Validation("agent.invalidsuspensionreason", "The suspension reason must be between 10 and 500 characters."));

        var agent = await dbContext.Agents.FirstOrDefaultAsync(a => a.Id == agentId, ct);

        if (agent is null)
            return Result<AgentSummaryDto>.Failure(
                Error.NotFound("agent.notfound", $"Agent with id {agentId} was not found."));

        // Idempotent. Keep the first reason, timestamp and decision-maker so a
        // repeated request cannot launder a decision, and report the agent as
        // suspended rather than as a conflict.
        if (agent.IsSuspended)
            return await GetSummaryAsync(agentId, includeSuspended: true, ct);

        agent.IsSuspended = true;
        agent.SuspendedAt = DateTime.UtcNow;
        agent.SuspensionReason = reason;
        agent.SuspendedByAdminId = adminUserId;
        agent.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<AgentSummaryDto>.Failure(Error.Concurrency());
        }

        // Summaries are read back with includeSuspended: true so the admin who
        // just made the decision sees the suspended state they set.
        return await GetSummaryAsync(agentId, includeSuspended: true, ct);
    }

    public async Task<Result<AgentSummaryDto>> ReinstateAsync(int agentId, CancellationToken ct)
    {
        var agent = await dbContext.Agents.FirstOrDefaultAsync(a => a.Id == agentId, ct);

        if (agent is null)
            return Result<AgentSummaryDto>.Failure(
                Error.NotFound("agent.notfound", $"Agent with id {agentId} was not found."));

        if (!agent.IsSuspended)
            return await GetSummaryAsync(agentId, includeSuspended: true, ct);

        // Cleared rather than archived: the suspension is a current-state flag
        // plus its audit stamp. The report that motivated it keeps its own
        // resolution history.
        agent.IsSuspended = false;
        agent.SuspendedAt = null;
        agent.SuspensionReason = null;
        agent.SuspendedByAdminId = null;
        agent.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<AgentSummaryDto>.Failure(Error.Concurrency());
        }

        return await GetSummaryAsync(agentId, includeSuspended: true, ct);
    }
}
