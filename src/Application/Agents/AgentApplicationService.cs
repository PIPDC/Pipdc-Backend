using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PIPDC.Application.Agents;
using PIPDC.Application.Auth;
using PIPDC.Application.Data;
using PIPDC.Application.Email;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Agents;

/// <summary>
/// Owns the agent-application boundary, for both the applicant and the reviewing
/// admin.
///
/// The applicant-facing surface can only ever create and read applications for
/// the authenticated user it belongs to: no user id is accepted from a request
/// body anywhere.
///
/// The admin-facing surface drives the review workflow. Approval grants the
/// Agent role and issues a licence; it deliberately does not verify. A second,
/// separate step verifies, so that publishing a licence to the public agent
/// directory is never a side effect of the same click that approved a person.
/// </summary>
public class AgentApplicationService(
    IAppDbContext dbContext,
    UserManager<AppUser> userManager,
    IAgentLicenseGenerator licenseGenerator,
    IEmailQueue emailQueue,
    IOptions<GmailApiSettings> gmailOptions,
    ILogger<AgentApplicationService> logger) : IAgentApplicationService
{
    public async Task<Result<AgentApplicationResponse>> SubmitAsync(
        string userId,
        AgentApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result<AgentApplicationResponse>.Failure(
                Error.Unauthorized("agentapplication.unauthenticated", "An authenticated account is required to apply."));

        var validation = Validate(request);
        if (validation is not null)
            return Result<AgentApplicationResponse>.Failure(validation);

        if (await dbContext.Agents.AnyAsync(a => a.UserId == userId, cancellationToken))
            return Result<AgentApplicationResponse>.Failure(
                Error.Conflict("agentapplication.alreadyagent", "This account is already registered as an agent."));

        var hasOpen = await dbContext.AgentApplications.AnyAsync(
            a => a.UserId == userId
                && (a.Status == AgentApplicationStatus.Submitted || a.Status == AgentApplicationStatus.UnderReview),
            cancellationToken);

        if (hasOpen)
            return Result<AgentApplicationResponse>.Failure(
                Error.Conflict("agentapplication.openexists", "You already have an application awaiting review."));

        var application = new AgentApplication
        {
            UserId = userId,
            FullName = request.FullName.Trim(),
            StateOfOrigin = request.StateOfOrigin.Trim(),
            ResidentialAddress = request.ResidentialAddress.Trim(),
            LocalGovernmentArea = request.LocalGovernmentArea.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            AgencyName = Normalize(request.AgencyName),
            AdditionalNotes = Normalize(request.AdditionalNotes),
            Status = AgentApplicationStatus.Submitted,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.AgentApplications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AgentApplicationResponse>.Success(Map(application));
    }

    public async Task<Result<IReadOnlyList<AgentApplicationResponse>>> GetMineAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result<IReadOnlyList<AgentApplicationResponse>>.Failure(
                Error.Unauthorized("agentapplication.unauthenticated", "An authenticated account is required."));

        var items = await dbContext.AgentApplications
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        IReadOnlyList<AgentApplicationResponse> responses = items.Select(Map).ToList();
        return Result<IReadOnlyList<AgentApplicationResponse>>.Success(responses);
    }

    public async Task<Result<AgentApplicationResponse?>> GetCurrentAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result<AgentApplicationResponse?>.Failure(
                Error.Unauthorized("agentapplication.unauthenticated", "An authenticated account is required."));

        var application = await dbContext.AgentApplications
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Result<AgentApplicationResponse?>.Success(application is null ? null : Map(application));
    }

    // ── Admin review workflow ────────────────────────────────────────────

    public async Task<Result<PagedResult<AgentApplicationReviewResponse>>> ListForReviewAsync(
        PagedQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var filtered = dbContext.AgentApplications
            .Include(a => a.User)
            .AsNoTracking();

        if (query.Status is { } status)
            filtered = filtered.Where(a => a.Status == status);

        var total = await filtered.CountAsync(cancellationToken);

        var items = await filtered
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        IReadOnlyList<AgentApplicationReviewResponse> responses = items.Select(MapReview).ToList();
        return Result<PagedResult<AgentApplicationReviewResponse>>.Success(
            new PagedResult<AgentApplicationReviewResponse>(responses, total, page, pageSize));
    }

    public async Task<Result<AgentApplicationReviewResponse>> GetForReviewAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var application = await dbContext.AgentApplications
            .Include(a => a.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (application is null)
            return Result<AgentApplicationReviewResponse>.Failure(
                Error.NotFound("agentapplication.notfound", $"Application {id} was not found."));

        return Result<AgentApplicationReviewResponse>.Success(MapReview(application));
    }

    public async Task<Result<AgentApplicationReviewResponse>> StartReviewAsync(
        int id,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var application = await dbContext.AgentApplications
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (application is null)
            return Failure<AgentApplicationReviewResponse>("agentapplication.notfound", $"Application {id} was not found.");

        if (application.Status != AgentApplicationStatus.Submitted)
            return Failure<AgentApplicationReviewResponse>(
                "agentapplication.notreviewable",
                $"Only a submitted application can be picked up for review. This one is {application.Status}.");

        application.Status = AgentApplicationStatus.UnderReview;
        application.ReviewedByAdminId = adminId;
        application.UpdatedAt = DateTime.UtcNow;

        var saveError = await TrySaveAsync(cancellationToken);
        if (saveError is not null)
            return Result<AgentApplicationReviewResponse>.Failure(saveError);

        return Result<AgentApplicationReviewResponse>.Success(MapReview(application));
    }

    public async Task<Result<AgentApplicationReviewResponse>> ApproveAsync(
        int id,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var application = await dbContext.AgentApplications
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (application is null)
            return Failure<AgentApplicationReviewResponse>("agentapplication.notfound", $"Application {id} was not found.");

        if (application.Status is not (AgentApplicationStatus.Submitted or AgentApplicationStatus.UnderReview))
            return Failure<AgentApplicationReviewResponse>(
                "agentapplication.notreviewable",
                $"Only a submitted or under-review application can be approved. This one is {application.Status}.");

        // Re-check rather than trusting the status alone. The role and the Agent
        // row are two separate stores, and a previous approval that failed
        // partway could have left the role granted with no Agent row.
        if (await dbContext.Agents.AnyAsync(a => a.UserId == application.UserId, cancellationToken))
            return Failure<AgentApplicationReviewResponse>(
                "agentapplication.alreadyagent", "This account is already registered as an agent.");

        var user = await userManager.FindByIdAsync(application.UserId);
        if (user is null)
            return Failure<AgentApplicationReviewResponse>(
                "agentapplication.usernotfound", "The applicant's account no longer exists.");

        // Issue the licence before mutating anything, so a generator failure
        // leaves the application untouched and retryable.
        var license = await licenseGenerator.GenerateAsync(cancellationToken);
        if (license.IsFailure)
            return Result<AgentApplicationReviewResponse>.Failure(license.Error);

        var agent = await dbContext.Agents.FirstOrDefaultAsync(a => a.UserId == application.UserId, cancellationToken);
        if (agent is null)
        {
            agent = new Agent
            {
                // Phone comes from the application, not the account, because the
                // applicant supplied it as part of being vetted.
                PhoneNumber = application.PhoneNumber,
                AgencyName = application.AgencyName ?? "PIPDC Agency",
                Title = "Agent",
                LicenseNumber = license.Value,
                // Approval is deliberately not verification.
                IsVerified = false,
                UserId = application.UserId,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Agents.Add(agent);
        }
        else
        {
            // An Agent row already exists (created by the legacy promote path).
            // Issue the licence so the agent is not left unlicenced, but do not
            // touch IsVerified: that is the verification step's call.
            agent.LicenseNumber = license.Value;
        }

        application.Status = AgentApplicationStatus.Approved;
        application.ReviewedAt = DateTime.UtcNow;
        application.ReviewedByAdminId = adminId;
        application.RejectionReason = null;
        application.UpdatedAt = DateTime.UtcNow;

        var roleResult = await userManager.AddToRoleAsync(user, Roles.Agent);
        if (!roleResult.Succeeded)
        {
            // The role grant and the database writes must not diverge. Returning
            // without saving leaves the application reviewable, so the admin can
            // retry, rather than half-applied.
            return Failure<AgentApplicationReviewResponse>(
                "agentapplication.rolegrantfailed",
                string.Join("; ", roleResult.Errors.Select(e => e.Description)));
        }

        var saveError = await TrySaveAsync(cancellationToken);
        if (saveError is not null)
        {
            // Undo the role grant so a failed save does not leave a user holding
            // the Agent role with no licence and no agent profile.
            var removeResult = await userManager.RemoveFromRoleAsync(user, Roles.Agent);
            if (!removeResult.Succeeded)
            {
                logger.LogError(
                    "Failed to roll back Agent role for {UserId} after approval save failure. The user holds the Agent role with no agent profile. Errors: {Errors}",
                    application.UserId,
                    string.Join("; ", removeResult.Errors.Select(e => e.Description)));
            }

            return Result<AgentApplicationReviewResponse>.Failure(saveError);
        }

        QueueApplicationEmail(
            application,
            EmailTemplates.AgentApplicationApproved(
                user.Email!,
                application.FullName,
                license.Value,
                gmailOptions.Value.FrontendBaseUrl));

        return Result<AgentApplicationReviewResponse>.Success(MapReview(application));
    }

    public async Task<Result<AgentApplicationReviewResponse>> RejectAsync(
        int id,
        string adminId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Failure<AgentApplicationReviewResponse>(
                "agentapplication.reasonrequired", "A rejection reason is required so the applicant knows why.");

        var trimmed = reason.Trim();
        if (trimmed.Length > 1000)
            return Failure<AgentApplicationReviewResponse>(
                "agentapplication.reasontoolong", "The rejection reason must be 1000 characters or fewer.");

        var application = await dbContext.AgentApplications
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (application is null)
            return Failure<AgentApplicationReviewResponse>("agentapplication.notfound", $"Application {id} was not found.");

        if (application.Status is not (AgentApplicationStatus.Submitted or AgentApplicationStatus.UnderReview))
            return Failure<AgentApplicationReviewResponse>(
                "agentapplication.notreviewable",
                $"Only a submitted or under-review application can be rejected. This one is {application.Status}.");

        application.Status = AgentApplicationStatus.Rejected;
        application.RejectionReason = trimmed;
        application.ReviewedAt = DateTime.UtcNow;
        application.ReviewedByAdminId = adminId;
        application.UpdatedAt = DateTime.UtcNow;

        var saveError = await TrySaveAsync(cancellationToken);
        if (saveError is not null)
            return Result<AgentApplicationReviewResponse>.Failure(saveError);

        QueueApplicationEmail(
            application,
            EmailTemplates.AgentApplicationRejected(
                application.User.Email!,
                application.FullName,
                trimmed,
                gmailOptions.Value.FrontendBaseUrl));

        return Result<AgentApplicationReviewResponse>.Success(MapReview(application));
    }

    /// <summary>
    /// Saves, translating the two failure modes this workflow cares about. A
    /// concurrency failure means a second admin got there first, which the caller
    /// should surface rather than silently overwrite. A unique-constraint
    /// violation on the licence index is a licence collision that a retry can
    /// resolve, so it is reported distinctly.
    /// </summary>
    private async Task<Error?> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error.Concurrency("Another administrator reviewed this application first. Reload and try again.");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Error.Conflict(
                "agentapplication.licencecollision",
                "A licence number collision occurred. Reload the application and approve again.");
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to save an agent application review");
            return Error.Failure("agentapplication.savefailed", "The review could not be saved. Please try again.");
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;

    private void QueueApplicationEmail(AgentApplication application, EmailMessage message)
    {
        if (string.IsNullOrWhiteSpace(application.User.Email))
        {
            logger.LogWarning(
                "Application {Id} was reviewed but the applicant has no email address; no notification sent.",
                application.Id);
            return;
        }

        // Best effort: the review is already committed at this point, so a
        // dropped notification must not fail the admin's action.
        if (emailQueue.Enqueue(message) == EnqueueResult.Dropped)
        {
            logger.LogError(
                "Email queue was full; the review notification for application {Id} was dropped.",
                application.Id);
        }
    }

    private static Result<T> Failure<T>(string code, string message) =>
        Result<T>.Failure(Error.Validation(code, message));

    private static Error? Validate(AgentApplicationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            return Error.Validation("agentapplication.fullname", "Full name is required.");

        if (request.FullName.Trim().Length > 200)
            return Error.Validation("agentapplication.fullname", "Full name must be 200 characters or fewer.");

        if (string.IsNullOrWhiteSpace(request.StateOfOrigin))
            return Error.Validation("agentapplication.stateoforigin", "State of origin is required.");

        if (string.IsNullOrWhiteSpace(request.ResidentialAddress))
            return Error.Validation("agentapplication.address", "Residential address is required.");

        if (string.IsNullOrWhiteSpace(request.LocalGovernmentArea))
            return Error.Validation("agentapplication.lga", "Local government area is required.");

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            return Error.Validation("agentapplication.phone", "Phone number is required.");

        if (request.PhoneNumber.Trim().Length > 20)
            return Error.Validation("agentapplication.phone", "Phone number must be 20 characters or fewer.");

        if (request.AgencyName is { Length: > 200 })
            return Error.Validation("agentapplication.agencyname", "Agency name must be 200 characters or fewer.");

        if (request.AdditionalNotes is { Length: > 2000 })
            return Error.Validation("agentapplication.notes", "Additional notes must be 2000 characters or fewer.");

        return null;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AgentApplicationReviewResponse MapReview(AgentApplication a) => new(
        a.Id,
        a.Status.ToString(),
        a.CreatedAt,
        a.UserId,
        a.User?.Email ?? string.Empty,
        // Surfaced so an admin can reject an application from an unverified
        // account, which can never receive the notification email.
        a.User?.EmailConfirmed == true ? a.User.Email : null,
        a.FullName,
        a.StateOfOrigin,
        a.ResidentialAddress,
        a.LocalGovernmentArea,
        a.PhoneNumber,
        a.AgencyName,
        a.AdditionalNotes,
        a.ReviewedAt,
        a.ReviewedByAdminId,
        a.RejectionReason);

    private static AgentApplicationResponse Map(AgentApplication a) => new(
        a.Id,
        a.Status.ToString(),
        a.CreatedAt,
        a.FullName,
        a.StateOfOrigin,
        a.ResidentialAddress,
        a.LocalGovernmentArea,
        a.PhoneNumber,
        a.AgencyName,
        a.AdditionalNotes,
        a.ReviewedAt,
        a.RejectionReason);
}
