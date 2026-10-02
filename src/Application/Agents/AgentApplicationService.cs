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
    IOptions<EmailSettings> emailOptions,
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

        // Removed rows are excluded on purpose. A revoked agent has to be able to
        // reapply, and testing the row instead of the state would block them here
        // with "already registered" while their own page says Revoked.
        if (await dbContext.Agents.AnyAsync(a => a.UserId == userId && !a.IsRemoved, cancellationToken))
            return Result<AgentApplicationResponse>.Failure(
                Error.Conflict("agentapplication.alreadyagent", "This account is already registered as an agent."));

        // A bar outranks everything else. It is checked before the open-application
        // test so a blocked applicant gets the real reason rather than being told to
        // wait for a review that will never happen.
        var block = await dbContext.AgentApplicationBlocks
            .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

        if (block is { LiftedAt: null })
            return Result<AgentApplicationResponse>.Failure(
                Error.Forbidden("agentapplication.blocked", block.Reason));

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
            PhoneNumber = request.PhoneNumber.Trim(),
            DateOfBirth = request.DateOfBirth?.Date,
            StateOfOrigin = request.StateOfOrigin.Trim(),
            LocalGovernmentArea = request.LocalGovernmentArea.Trim(),
            ResidentialAddress = request.ResidentialAddress.Trim(),
            NationalIdentityNumber = NormalizeNin(request.NationalIdentityNumber),
            YearsOfExperience = request.YearsOfExperience,
            AgencyName = Normalize(request.AgencyName),
            AdditionalNotes = Normalize(request.AdditionalNotes),
            Status = AgentApplicationStatus.Submitted,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.AgentApplications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken);

        NotifyApplicantAndAdmins(application, userId, cancellationToken);

        return Result<AgentApplicationResponse>.Success(Map(application));
    }

    /// <summary>
    /// Confirms receipt to the applicant and alerts the review team. Both are
    /// best effort: the application is already committed, so a full queue or an
    /// unconfirmed address must not fail the submission the applicant just made.
    /// </summary>
    private void NotifyApplicantAndAdmins(
        AgentApplication application,
        string userId,
        CancellationToken cancellationToken)
    {
        var user = userManager.Users
            .FirstOrDefault(u => u.Id == userId);

        if (user is null)
        {
            logger.LogWarning(
                "Application {Id} was submitted but the applicant account could not be reloaded; no notification sent.",
                application.Id);
            return;
        }

        // An unconfirmed address cannot receive mail, so the applicant is not
        // told anything is on its way. The admin alert still fires, because the
        // review queue is the fallback path.
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            logger.LogWarning(
                "Application {Id} was submitted by an account with no email address.",
                application.Id);
        }
        else if (user.EmailConfirmed == true)
        {
            emailQueue.QueueEmail(
                logger,
                EmailTemplates.AgentApplicationReceived(
                    user.Email,
                    application.FullName,
                    gmailOptions.Value.FrontendBaseUrl),
                "agent-application-received",
                cancellationToken);
        }
        else
        {
            logger.LogInformation(
                "Application {Id} acknowledgement skipped: the applicant's email is not confirmed.",
                application.Id);
        }

        var adminRecipient = emailOptions.Value.ResolveAgentApplicationsRecipient();
        if (string.IsNullOrWhiteSpace(adminRecipient))
        {
            logger.LogWarning(
                "Application {Id} was submitted but no Email:AgentApplicationsRecipient or Email:ContactRecipient is configured; admins were not notified.",
                application.Id);
            return;
        }

        emailQueue.QueueEmail(
            logger,
            EmailTemplates.NewAgentApplicationToAdmin(
                adminRecipient,
                application.FullName,
                user.Email ?? "not recorded",
                application.PhoneNumber,
                application.StateOfOrigin,
                application.LocalGovernmentArea,
                application.YearsOfExperience ?? 0,
                application.CreatedAt,
                gmailOptions.Value.FrontendBaseUrl),
            "agent-application-admin-alert",
            cancellationToken);
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

    public async Task<Result<AgentApplicationEligibilityResponse>> GetEligibilityAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result<AgentApplicationEligibilityResponse>.Failure(
                Error.Unauthorized("agentapplication.unauthenticated", "An authenticated account is required."));

        var isAgent = await dbContext.Agents
            .AnyAsync(a => a.UserId == userId && !a.IsRemoved, cancellationToken);

        var block = await dbContext.AgentApplicationBlocks
            .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

        var isBlocked = block is { LiftedAt: null };

        var hasOpenApplication = await dbContext.AgentApplications.AnyAsync(
            a => a.UserId == userId
                && (a.Status == AgentApplicationStatus.Submitted || a.Status == AgentApplicationStatus.UnderReview),
            cancellationToken);

        var hasRevokedRegistration = await dbContext.AgentApplications.AnyAsync(
            a => a.UserId == userId && a.Status == AgentApplicationStatus.Revoked,
            cancellationToken);

        var hasOpenAppeal = await dbContext.AgentRegistrationAppeals.AnyAsync(
            a => a.UserId == userId
                && (a.Status == AgentAppealStatus.Submitted || a.Status == AgentAppealStatus.UnderReview),
            cancellationToken);

        // A bar is permanent and outranks everything: the form must not be offered
        // to someone who cannot use it.
        var canApply = !isBlocked && !isAgent && !hasOpenApplication;

        return Result<AgentApplicationEligibilityResponse>.Success(new AgentApplicationEligibilityResponse(
            canApply,
            isBlocked,
            // The reason is shown to the applicant, so the bar's own wording is the
            // message. Fall back to a generic one if an old row predates the column.
            isBlocked ? (block!.Reason.Length > 0 ? block.Reason : "You are not permitted to apply as an agent on PIPDC.") : null,
            isAgent,
            hasOpenApplication,
            hasRevokedRegistration,
            // An appeal is only meaningful against a revoked registration, and only
            // one may be open at a time.
            CanAppeal: hasRevokedRegistration && !hasOpenAppeal && !isAgent,
            HasOpenAppeal: hasOpenAppeal));
    }

    public async Task<Result<IReadOnlyList<AgentAppealResponse>>> GetMyAppealsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result<IReadOnlyList<AgentAppealResponse>>.Failure(
                Error.Unauthorized("agentapplication.unauthenticated", "An authenticated account is required."));

        var items = await dbContext.AgentRegistrationAppeals
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        IReadOnlyList<AgentAppealResponse> responses = items.Select(MapAppeal).ToList();
        return Result<IReadOnlyList<AgentAppealResponse>>.Success(responses);
    }

    public async Task<Result<AgentAppealResponse>> SubmitAppealAsync(
        string userId,
        SubmitAgentAppealRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result<AgentAppealResponse>.Failure(
                Error.Unauthorized("agentapplication.unauthenticated", "An authenticated account is required."));

        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is < 20 or > 4000)
            return Result<AgentAppealResponse>.Failure(Error.Validation(
                "agentappeal.invalidreason",
                "Your appeal must be between 20 and 4000 characters so there is something for an administrator to consider."));

        // The appeal has to contest something. Without a revoked application there
        // is no decision to appeal.
        var revokedApplication = await dbContext.AgentApplications
            .Where(a => a.UserId == userId && a.Status == AgentApplicationStatus.Revoked)
            .OrderByDescending(a => a.RevokedAt ?? a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (revokedApplication is null)
            return Result<AgentAppealResponse>.Failure(Error.Conflict(
                "agentappeal.notrevoked",
                "There is no revoked agent registration on your account to appeal."));

        // Being made an agent again by some other route, or still holding a live
        // registration, makes an appeal meaningless.
        var liveAgent = await dbContext.Agents
            .FirstOrDefaultAsync(a => a.UserId == userId && !a.IsRemoved, cancellationToken);

        if (liveAgent is not null)
            return Result<AgentAppealResponse>.Failure(Error.Conflict(
                "agentappeal.alreadyagent",
                "Your account is already registered as an agent, so there is nothing to appeal."));

        var alreadyOpen = await dbContext.AgentRegistrationAppeals.AnyAsync(
            a => a.UserId == userId
                && (a.Status == AgentAppealStatus.Submitted || a.Status == AgentAppealStatus.UnderReview),
            cancellationToken);

        if (alreadyOpen)
            return Result<AgentAppealResponse>.Failure(Error.Conflict(
                "agentappeal.openexists",
                "You already have an appeal awaiting a decision."));

        var user = await userManager.FindByIdAsync(userId);

        var appeal = new AgentRegistrationAppeal
        {
            AgentApplicationId = revokedApplication.Id,
            UserId = userId,
            Reason = reason,
            Status = AgentAppealStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.AgentRegistrationAppeals.Add(appeal);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // The partial unique index caught a concurrent second appeal.
            return Result<AgentAppealResponse>.Failure(Error.Conflict(
                "agentappeal.openexists",
                "You already have an appeal awaiting a decision."));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<AgentAppealResponse>.Failure(Error.Concurrency());
        }

        var adminRecipient = emailOptions.Value.ResolveAgentApplicationsRecipient();
        if (!string.IsNullOrWhiteSpace(adminRecipient) && user is not null)
        {
            emailQueue.QueueEmail(
                logger,
                EmailTemplates.AgentAppealLodgedToAdmin(
                    adminRecipient,
                    user.FullName ?? user.UserName ?? "An agent",
                    user.Email ?? "not recorded",
                    reason,
                    appeal.CreatedAt,
                    gmailOptions.Value.FrontendBaseUrl),
                "agent-appeal-admin-alert",
                cancellationToken);
        }

        return Result<AgentAppealResponse>.Success(MapAppeal(appeal));
    }

    public async Task<Result<PagedResult<AgentAppealReviewResponse>>> ListAppealsAsync(
        AgentAppealQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var filtered = dbContext.AgentRegistrationAppeals
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

        IReadOnlyList<AgentAppealReviewResponse> responses = items.Select(MapAppealReview).ToList();
        return Result<PagedResult<AgentAppealReviewResponse>>.Success(
            new PagedResult<AgentAppealReviewResponse>(responses, total, page, pageSize));
    }

    public async Task<Result<AgentAppealReviewResponse>> GetAppealAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var appeal = await dbContext.AgentRegistrationAppeals
            .Include(a => a.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return appeal is null
            ? Result<AgentAppealReviewResponse>.Failure(
                Error.NotFound("agentappeal.notfound", $"Appeal {id} was not found."))
            : Result<AgentAppealReviewResponse>.Success(MapAppealReview(appeal));
    }

    public async Task<Result<AgentAppealReviewResponse>> StartAppealReviewAsync(
        int id,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var appeal = await dbContext.AgentRegistrationAppeals
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (appeal is null)
            return Failure<AgentAppealReviewResponse>("agentappeal.notfound", $"Appeal {id} was not found.");

        if (appeal.Status != AgentAppealStatus.Submitted)
            return Failure<AgentAppealReviewResponse>(
                "agentappeal.notreviewable",
                $"Only a submitted appeal can be picked up for review. This one is {appeal.Status}.");

        appeal.Status = AgentAppealStatus.UnderReview;
        appeal.ReviewedByAdminId = adminId;
        appeal.UpdatedAt = DateTime.UtcNow;

        var saveError = await TrySaveAsync(cancellationToken);
        if (saveError is not null)
            return Result<AgentAppealReviewResponse>.Failure(saveError);

        var loaded = await dbContext.AgentRegistrationAppeals
            .Include(a => a.User)
            .AsNoTracking()
            .FirstAsync(a => a.Id == id, cancellationToken);

        return Result<AgentAppealReviewResponse>.Success(MapAppealReview(loaded));
    }

    public async Task<Result<AgentAppealReviewResponse>> ResolveAppealAsync(
        int id,
        string adminId,
        ResolveAgentAppealRequest request,
        CancellationToken cancellationToken = default)
    {
        var decision = request.Decision?.Trim() ?? string.Empty;
        if (!string.Equals(decision, AgentAppealDecisions.Upheld, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(decision, AgentAppealDecisions.Refused, StringComparison.OrdinalIgnoreCase))
        {
            return Failure<AgentAppealReviewResponse>(
                "agentappeal.invaliddecision",
                $"The decision must be either {AgentAppealDecisions.Upheld} or {AgentAppealDecisions.Refused}.");
        }

        var note = request.Note?.Trim() ?? string.Empty;
        if (note.Length is < 10 or > 2000)
            return Failure<AgentAppealReviewResponse>(
                "agentappeal.invalidnote",
                "A decision note between 10 and 2000 characters is required so the agent knows the outcome.");

        var upheld = string.Equals(decision, AgentAppealDecisions.Upheld, StringComparison.OrdinalIgnoreCase);

        var appeal = await dbContext.AgentRegistrationAppeals
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (appeal is null)
            return Failure<AgentAppealReviewResponse>("agentappeal.notfound", $"Appeal {id} was not found.");

        if (appeal.Status is not (AgentAppealStatus.Submitted or AgentAppealStatus.UnderReview))
            return Failure<AgentAppealReviewResponse>(
                "agentappeal.notdecidable",
                $"This appeal has already been decided. It is {appeal.Status}.");

        appeal.Status = upheld ? AgentAppealStatus.Upheld : AgentAppealStatus.Refused;
        appeal.ReviewedAt = DateTime.UtcNow;
        appeal.ReviewedByAdminId = adminId;
        appeal.DecisionNote = note;
        appeal.UpdatedAt = DateTime.UtcNow;

        string licenceNumber = string.Empty;

        // Tracks whether *this* call granted the role, so a failed save can take it
        // back. Restoring an existing role must never be undone, because the agent
        // may have held it independently of this appeal.
        AppUser? roleGrantedUser = null;

        if (upheld)
        {
            // Reinstate the same registration rather than creating a new agent, so
            // the licence, the review history and the property count all survive.
            var agent = await dbContext.Agents
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.UserId == appeal.UserId, cancellationToken);

            if (agent is null)
            {
                return Failure<AgentAppealReviewResponse>(
                    "agentappeal.agentgone",
                    "This agent's record no longer exists, so the appeal cannot be upheld against it. Refuse the appeal instead.");
            }

            agent.IsRemoved = false;
            agent.RemovedAt = null;
            agent.RemovalReason = null;
            agent.RemovedByAdminId = null;
            agent.ReinstatedAt = DateTime.UtcNow;
            agent.UpdatedAt = DateTime.UtcNow;
            licenceNumber = agent.LicenseNumber ?? string.Empty;

            // The application that granted the registration becomes true again, so
            // the applicant's page stops saying it was revoked.
            if (appeal.AgentApplicationId is { } applicationId)
            {
                var application = await dbContext.AgentApplications
                    .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

                if (application is not null)
                {
                    application.Status = AgentApplicationStatus.Approved;
                    application.RevocationReason = null;
                    application.RevokedAt = null;
                    application.RevokedByAdminId = null;
                    application.UpdatedAt = DateTime.UtcNow;
                }
            }

            // ReassignedToAgentId is deliberately left as-is. It records that the
            // admin moved this agent's work to a successor, and that successor may
            // have transacted with it since. Upholding an appeal reinstates the
            // person; it does not silently take live listings back off someone who
            // was given them.

            // The role was dropped at removal, so it has to be granted again.
            var user = await userManager.FindByIdAsync(appeal.UserId);
            if (user is not null && !await userManager.IsInRoleAsync(user, Roles.Agent))
            {
                var roleResult = await userManager.AddToRoleAsync(user, Roles.Agent);
                if (!roleResult.Succeeded)
                {
                    // Returning before the save leaves the appeal reviewable, so the
                    // admin can retry. Recording "Upheld" for an agent who cannot
                    // reach their dashboard is precisely the stale-state bug this
                    // flow exists to remove.
                    return Failure<AgentAppealReviewResponse>(
                        "agentappeal.rolegrantfailed",
                        string.Join("; ", roleResult.Errors.Select(e => e.Description)));
                }

                roleGrantedUser = user;
            }
        }

        var saveError = await TrySaveAsync(cancellationToken);
        if (saveError is not null)
        {
            // Identity is a separate store, so the grant above survives a failed
            // database save. Left in place, the agent would hold the Agent role while
            // their registration is still revoked and this appeal is still open:
            // access granted, record not reinstated. Undo the grant so a retry starts
            // from the state the admin actually decided, not a half-applied one.
            if (roleGrantedUser is not null)
            {
                var rollback = await userManager.RemoveFromRoleAsync(roleGrantedUser, Roles.Agent);
                if (!rollback.Succeeded)
                {
                    logger.LogError(
                        "Failed to roll back the Agent role for user {UserId} after appeal {AppealId} failed to save: {Errors}",
                        appeal.UserId,
                        appeal.Id,
                        string.Join("; ", rollback.Errors.Select(e => e.Description)));
                }
            }

            return Result<AgentAppealReviewResponse>.Failure(saveError);
        }

        var email = appeal.User?.Email;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var name = appeal.User?.FullName ?? "Agent";
            var message = upheld
                ? EmailTemplates.AgentAppealUpheld(email, name, licenceNumber, gmailOptions.Value.FrontendBaseUrl)
                // A refused appeal only advertises reapplying if the bar allows it,
                // so the email cannot promise something the API will refuse.
                : EmailTemplates.AgentAppealRefused(
                    email,
                    name,
                    note,
                    mayReapply: !await dbContext.AgentApplicationBlocks.AnyAsync(
                        b => b.UserId == appeal.UserId && b.LiftedAt == null, cancellationToken),
                    gmailOptions.Value.FrontendBaseUrl);

            emailQueue.QueueEmail(logger, message, "agent-appeal-outcome", cancellationToken);
        }

        var refreshed = await dbContext.AgentRegistrationAppeals
            .Include(a => a.User)
            .AsNoTracking()
            .FirstAsync(a => a.Id == id, cancellationToken);

        return Result<AgentAppealReviewResponse>.Success(MapAppealReview(refreshed));
    }

    public async Task<Result> BlockFromApplyingAsync(
        int applicationId,
        string adminId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length is < 10 or > 1000)
            return Result.Failure(Error.Validation(
                "agentapplication.invalidblockreason",
                "A reason between 10 and 1000 characters is required so the applicant knows why."));

        var application = await dbContext.AgentApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

        if (application is null)
            return Result.Failure(Error.NotFound(
                "agentapplication.notfound", $"Application {applicationId} was not found."));

        if (application.Status is not (AgentApplicationStatus.Rejected or AgentApplicationStatus.Revoked))
            return Result.Failure(Error.Conflict(
                "agentapplication.notblockable",
                "Only a rejected or revoked application can have its applicant barred from reapplying."));

        // Idempotent on the row that exists, but a lifted bar is genuinely
        // re-raisable, which is why this looks at LiftedAt rather than just existence.
        var existing = await dbContext.AgentApplicationBlocks
            .FirstOrDefaultAsync(b => b.UserId == application.UserId, cancellationToken);

        if (existing is { LiftedAt: null })
            return Result.Success();

        if (existing is not null)
        {
            existing.Reason = trimmed;
            existing.LiftedAt = null;
            existing.LiftedByAdminId = null;
            existing.UpdatedAt = DateTime.UtcNow;
            // Re-raised, so the original decision-maker is no longer the right
            // attribution for who set the bar now.
            existing.CreatedBy = adminId;
            existing.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            dbContext.AgentApplicationBlocks.Add(new AgentApplicationBlock
            {
                UserId = application.UserId,
                Reason = trimmed,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = adminId
            });
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Result.Failure(Error.Conflict(
                "agentapplication.blockexists",
                "This account is already barred from applying."));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Concurrency());
        }

        var user = await userManager.FindByIdAsync(application.UserId);
        if (user is not null && !string.IsNullOrWhiteSpace(user.Email))
        {
            emailQueue.QueueEmail(
                logger,
                EmailTemplates.AgentApplicationBlocked(
                    user.Email,
                    user.FullName ?? user.UserName ?? "Applicant",
                    trimmed,
                    gmailOptions.Value.FrontendBaseUrl),
                "agent-application-blocked",
                cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> LiftApplicationBlockAsync(
        string userId,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure(
                Error.Unauthorized("agentapplication.unauthenticated", "An authenticated account is required."));

        // Keyed on the account, not an application: the bar outlives any single
        // application, and a barred applicant may have several.
        var block = await dbContext.AgentApplicationBlocks
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

        if (block is null)
            return Result.Failure(Error.NotFound(
                "agentapplication.notblocked", "This account is not barred from applying."));

        // Idempotent: lifting an already-lifted bar is a no-op, not an error.
        if (block.LiftedAt is not null)
            return Result.Success();

        block.LiftedAt = DateTime.UtcNow;
        block.LiftedByAdminId = adminId;
        block.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Concurrency());
        }

        if (block.User is { } barredUser && !string.IsNullOrWhiteSpace(barredUser.Email))
        {
            emailQueue.QueueEmail(
                logger,
                EmailTemplates.AgentApplicationBlockLifted(
                    barredUser.Email,
                    barredUser.FullName ?? barredUser.UserName ?? "Applicant",
                    gmailOptions.Value.FrontendBaseUrl),
                "agent-application-unblocked",
                cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<AgentApplicationBlockResponse>>> ListApplicationBlocksAsync(
        CancellationToken cancellationToken = default)
    {
        var blocks = await dbContext.AgentApplicationBlocks
            .Include(b => b.User)
            .AsNoTracking()
            .OrderBy(b => b.LiftedAt == null ? 0 : 1)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        IReadOnlyList<AgentApplicationBlockResponse> responses = blocks.Select(b => new AgentApplicationBlockResponse(
            b.Id,
            b.UserId,
            b.User?.FullName ?? "Unknown user",
            b.User?.Email ?? string.Empty,
            b.Reason,
            b.CreatedAt,
            // Set from the JWT subject claim by the API layer; the column defaults to
            // empty rather than null so the audit trail is never ambiguous.
            string.IsNullOrWhiteSpace(b.CreatedBy) ? "unknown" : b.CreatedBy,
            b.LiftedAt,
            b.LiftedByAdminId)).ToList();

        return Result<IReadOnlyList<AgentApplicationBlockResponse>>.Success(responses);
    }

    public async Task<Result> WithdrawRejectedAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure(
                Error.Unauthorized("agentapplication.unauthenticated", "An authenticated account is required."));

        var application = await dbContext.AgentApplications
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (application is null)
            return Result.Failure(
                Error.NotFound("agentapplication.notfound", "You do not have an application to withdraw."));

        if (application.Status != AgentApplicationStatus.Rejected)
            return Result.Failure(Error.Conflict(
                "agentapplication.notwithdrawable",
                application.Status == AgentApplicationStatus.Approved
                    ? "This application was approved, so it cannot be withdrawn."
                    : "This application is still being reviewed, so it cannot be withdrawn."));

        dbContext.AgentApplications.Remove(application);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Concurrency(
                "This application changed while you were viewing it. Reload and try again."));
        }

        return Result.Success();
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
        // partway could have left the role granted with no Agent row. Removed rows
        // are excluded: a reapplying former agent is not "already registered", and
        // the existing row is revived further down rather than duplicated.
        if (await dbContext.Agents.AnyAsync(a => a.UserId == application.UserId && !a.IsRemoved, cancellationToken))
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
        else if (agent.IsRemoved)
        {
            // A reapplication from a removed agent. The same row is brought back
            // rather than a second one being created, because the row is what the
            // properties, enquiries, reviews and reports already point at. A fresh
            // licence is issued either way: a revoked agent must not keep the old
            // one. IsVerified is deliberately not set, because approval is not
            // verification.
            agent.LicenseNumber = license.Value;
            agent.IsRemoved = false;
            agent.RemovedAt = null;
            agent.RemovalReason = null;
            agent.RemovedByAdminId = null;
            agent.ReinstatedAt = DateTime.UtcNow;
            agent.UpdatedAt = DateTime.UtcNow;
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

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            return Error.Validation("agentapplication.phone", "Phone number is required.");

        if (request.PhoneNumber.Trim().Length > 20)
            return Error.Validation("agentapplication.phone", "Phone number must be 20 characters or fewer.");

        if (request.DateOfBirth is not { } dob)
            return Error.Validation("agentapplication.dob", "Date of birth is required.");

        if (dob > DateTime.UtcNow.Date)
            return Error.Validation("agentapplication.dob", "Date of birth cannot be in the future.");

        // Agents are adults holding a licence, so an applicant under 18 is
        // rejected at the boundary rather than at review.
        var eighteenthBirthday = dob.AddYears(18);
        if (eighteenthBirthday > DateTime.UtcNow.Date)
        {
            var age = DateTime.UtcNow.Year - dob.Year;
            if (dob.Day > DateTime.UtcNow.Day) age--;
            return Error.Validation(
                "agentapplication.dob", $"You must be 18 or older to apply. You are {age}.");
        }

        if (string.IsNullOrWhiteSpace(request.StateOfOrigin))
            return Error.Validation("agentapplication.stateoforigin", "State of origin is required.");

        if (request.StateOfOrigin.Trim().Length > 100)
            return Error.Validation("agentapplication.stateoforigin", "State of origin must be 100 characters or fewer.");

        if (string.IsNullOrWhiteSpace(request.LocalGovernmentArea))
            return Error.Validation("agentapplication.lga", "Local government area is required.");

        if (request.LocalGovernmentArea.Trim().Length > 200)
            return Error.Validation("agentapplication.lga", "Local government area must be 200 characters or fewer.");

        if (string.IsNullOrWhiteSpace(request.ResidentialAddress))
            return Error.Validation("agentapplication.address", "Residential address is required.");

        if (request.ResidentialAddress.Trim().Length > 500)
            return Error.Validation("agentapplication.address", "Residential address must be 500 characters or fewer.");

        if (string.IsNullOrWhiteSpace(request.NationalIdentityNumber))
            return Error.Validation("agentapplication.nin", "Your government ID number is required.");

        // Spaces and the hyphens people copy in are stripped before the shape is
        // checked, so "1234 5678 901" is accepted rather than bounced back at an
        // applicant who cannot correct it without starting the form again.
        var nin = NormalizeNin(request.NationalIdentityNumber);
        if (nin is { Length: > 20 })
            return Error.Validation("agentapplication.nin", "Government ID number must be 20 characters or fewer.");

        if (!IsDigitsOnly(nin))
            return Error.Validation("agentapplication.nin", "Government ID number must contain digits only.");

        if (nin.Length != 11)
            return Error.Validation("agentapplication.nin", "Government ID number must be 11 digits.");

        if (request.YearsOfExperience < 0)
            return Error.Validation("agentapplication.experience", "Years of experience cannot be negative.");

        if (request.YearsOfExperience > 80)
            return Error.Validation("agentapplication.experience", "Years of experience must be 80 or fewer.");

        if (request.AgencyName is { Length: > 200 })
            return Error.Validation("agentapplication.agencyname", "Agency name must be 200 characters or fewer.");

        if (request.AdditionalNotes is { Length: > 2000 })
            return Error.Validation("agentapplication.notes", "Additional notes must be 2000 characters or fewer.");

        return null;
    }

    private static bool IsDigitsOnly(string value)
    {
        foreach (var c in value)
        {
            if (!char.IsAsciiDigit(c))
                return false;
        }

        return value.Length > 0;
    }

    /// <summary>Strips whitespace and the separators a NIN is commonly written with.</summary>
    private static string NormalizeNin(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;

        foreach (var c in value)
        {
            if (char.IsAsciiDigit(c))
                buffer[length++] = c;
            else if (c is ' ' or '-' or '\t')
                continue;
            else
                // Anything else is kept, so validation can reject it with a clear
                // message rather than silently discarding the applicant's input.
                buffer[length++] = c;
        }

        return new string(buffer[..length]);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AgentAppealResponse MapAppeal(AgentRegistrationAppeal a) => new(
        a.Id,
        a.AgentApplicationId,
        a.Status.ToString(),
        a.Reason,
        a.CreatedAt,
        a.ReviewedAt,
        a.DecisionNote);

    private static AgentAppealReviewResponse MapAppealReview(AgentRegistrationAppeal a) => new(
        a.Id,
        a.AgentApplicationId,
        a.UserId,
        a.User?.FullName ?? "Unknown user",
        a.User?.Email ?? string.Empty,
        a.Status.ToString(),
        a.Reason,
        a.CreatedAt,
        a.ReviewedAt,
        a.ReviewedByAdminId,
        a.DecisionNote);

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
        a.PhoneNumber,
        a.DateOfBirth,
        a.StateOfOrigin,
        a.LocalGovernmentArea,
        a.ResidentialAddress,
        // Returned unmasked: this projection is admin-only and the reviewer
        // cannot vet identity without it.
        a.NationalIdentityNumber ?? string.Empty,
        a.YearsOfExperience,
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
        a.PhoneNumber,
        a.DateOfBirth,
        a.StateOfOrigin,
        a.LocalGovernmentArea,
        a.ResidentialAddress,
        a.NationalIdentityNumber ?? string.Empty,
        a.YearsOfExperience,
        a.AgencyName,
        a.AdditionalNotes,
        a.ReviewedAt,
        a.RejectionReason,
        a.RevocationReason,
        a.RevokedAt);
}
