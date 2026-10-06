using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PIPDC.Application.Auth;
using PIPDC.Application.Common;
using PIPDC.Application.Data;
using PIPDC.Application.Email;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Conversations;

public class ConversationEscalationService(
    IAppDbContext dbContext,
    IMessageNotifier notifier,
    IEmailQueue emailQueue,
    IOptions<GmailApiSettings> gmailOptions,
    IOptions<EmailSettings> emailOptions,
    ILogger<ConversationEscalationService> logger) : IConversationEscalationService
{
    public async Task<Result<PaginatedResult<ConversationDto>>> GetEscalationsAsync(
        string currentUserId, IList<string> currentUserRoles, ConversationQueryParameters q, CancellationToken ct)
    {
        if (!currentUserRoles.Contains(Roles.Admin))
            return Result<PaginatedResult<ConversationDto>>.Failure(
                Error.Forbidden("escalation.admironly", "Only an administrator can view the escalation queue."));

        // Unclaimed work first, then the caller's own open cases, then resolved
        // history so the queue doubles as an audit trail. Resolved rows are kept
        // out of the default view by the status filter below only when asked.
        var query = dbContext.Conversations
            .Where(c => c.EscalationStatus != ConversationEscalationStatus.Active);

        var statusFilter = ParseStatus(q.Status);
        if (statusFilter is not null)
            query = query.Where(c => c.EscalationStatus == statusFilter);

        var totalCount = await query.CountAsync(ct);

        // Ordered on the entity query, before projection: the projection carries the
        // status as a string for the frontend, so the typed comparison has to happen
        // while the enum is still available. Unclaimed and claimed work sorts above
        // resolved history, which stays in the queue as an audit trail.
        var ordered = query
            .OrderBy(c => c.EscalationStatus == ConversationEscalationStatus.Resolved ? 1 : 0)
            .ThenByDescending(c => c.EscalatedAt ?? c.CreatedAt);

        var items = await ConversationProjections.Project(ordered, currentUserId)
            .Skip((q.PageNumber - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync(ct);

        return Result<PaginatedResult<ConversationDto>>.Success(
            PaginatedResult<ConversationDto>.Create(
                items.Select(ConversationProjections.ToDto).ToList(), totalCount, q.PageNumber, q.PageSize));
    }

    public async Task<Result<ConversationDto>> EscalateAsync(
        int conversationId, EscalateConversationRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length < 10)
            return Result<ConversationDto>.Failure(
                Error.Validation("escalation.reasonrequired", "Give a reason of at least 10 characters so the administrator knows what happened."));

        var conversation = await dbContext.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (conversation is null)
            return Result<ConversationDto>.Failure(
                Error.NotFound("conversation.notfound", $"Conversation with id {conversationId} was not found."));

        var allowed = await ConversationAuthorization.AuthorizeEscalateAsync(dbContext, conversation, currentUserId, currentUserRoles, ct);
        if (allowed.IsFailure)
            return Result<ConversationDto>.Failure(allowed.Error);

        var now = DateTime.UtcNow;

        // Re-escalating an already-escalated conversation refreshes the reason and
        // the timestamp rather than creating a second escalation, so there is
        // only ever one owner and one reason per conversation.
        conversation.EscalationStatus = ConversationEscalationStatus.Escalated;
        conversation.EscalatedByUserId = currentUserId;
        conversation.EscalatedAt = now;
        conversation.EscalationReason = reason;
        conversation.AssignedAdminId = null;
        conversation.AssignedAt = null;
        conversation.UpdatedAt = now;

        await dbContext.SaveChangesAsync(ct);

        var dto = await ConversationProjections.SingleAsync(dbContext, conversationId, currentUserId, ct);

        await PublishEscalationChangeAsync(dto, ct);
        NotifyAdminsAsync(dto, ct);
        NotifyClientAsync(dto, ConversationEscalationStatus.Escalated, ct);

        return Result<ConversationDto>.Success(dto);
    }

    public async Task<Result<ConversationDto>> ClaimAsync(
        int conversationId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var conversation = await dbContext.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (conversation is null)
            return Result<ConversationDto>.Failure(
                Error.NotFound("conversation.notfound", $"Conversation with id {conversationId} was not found."));

        var allowed = ConversationAuthorization.AuthorizeClaim(conversation, currentUserRoles);
        if (allowed.IsFailure)
            return Result<ConversationDto>.Failure(allowed.Error);

        var now = DateTime.UtcNow;
        conversation.EscalationStatus = ConversationEscalationStatus.Assigned;
        conversation.AssignedAdminId = currentUserId;
        conversation.AssignedAt = now;
        conversation.UpdatedAt = now;

        await dbContext.SaveChangesAsync(ct);

        var dto = await ConversationProjections.SingleAsync(dbContext, conversationId, currentUserId, ct);

        await PublishEscalationChangeAsync(dto, ct);
        NotifyClientAsync(dto, ConversationEscalationStatus.Assigned, ct);

        return Result<ConversationDto>.Success(dto);
    }

    public async Task<Result<ConversationDto>> ResolveAsync(
        int conversationId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var conversation = await dbContext.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (conversation is null)
            return Result<ConversationDto>.Failure(
                Error.NotFound("conversation.notfound", $"Conversation with id {conversationId} was not found."));

        var allowed = ConversationAuthorization.AuthorizeResolve(conversation, currentUserId, currentUserRoles);
        if (allowed.IsFailure)
            return Result<ConversationDto>.Failure(allowed.Error);

        var now = DateTime.UtcNow;
        conversation.EscalationStatus = ConversationEscalationStatus.Resolved;
        conversation.ResolvedAt = now;
        conversation.ResolvedByUserId = currentUserId;
        conversation.UpdatedAt = now;

        await dbContext.SaveChangesAsync(ct);

        var dto = await ConversationProjections.SingleAsync(dbContext, conversationId, currentUserId, ct);

        await PublishEscalationChangeAsync(dto, ct);
        NotifyClientAsync(dto, ConversationEscalationStatus.Resolved, ct);

        return Result<ConversationDto>.Success(dto);
    }

    private static ConversationEscalationStatus? ParseStatus(string? status) =>
        Enum.TryParse<ConversationEscalationStatus>(status, ignoreCase: true, out var parsed) ? parsed : null;

    // Best-effort, exactly like MessageService's broadcast: the decision is already
    // committed, so a SignalR failure must not turn a successful write into a failed
    // request and invite the client to retry a state change that already happened.
    private async Task PublishEscalationChangeAsync(ConversationDto dto, CancellationToken ct)
    {
        try
        {
            await notifier.NotifyEscalationChangedAsync(dto, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "SignalR escalation broadcast failed for conversation {ConversationId}; the change was already persisted.",
                dto.Id);
        }
    }

    // Reuses Batch 2's queue and the same configured admin recipient the agent
    // application review queue uses, so there is one notification path, not two.
    // QueueEmail is fire-and-forget by design, so this is synchronous.
    private void NotifyAdminsAsync(ConversationDto dto, CancellationToken ct)
    {
        try
        {
            var recipient = emailOptions.Value.ResolveAgentApplicationsRecipient();
            if (string.IsNullOrWhiteSpace(recipient))
            {
                logger.LogWarning(
                    "Conversation {Id} was escalated but no Email:AgentApplicationsRecipient or Email:ContactRecipient is configured; admins were not notified.",
                    dto.Id);
                return;
            }

            emailQueue.QueueEmail(
                logger,
                EmailTemplates.ConversationEscalatedToAdmin(
                    recipient,
                    dto.Client.FullName,
                    dto.Agent.FullName,
                    dto.Property.Title,
                    dto.EscalationReason ?? "No reason recorded.",
                    gmailOptions.Value.FrontendBaseUrl),
                $"conversation-escalated:{dto.Id}",
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to queue escalation email for conversation {ConversationId}.", dto.Id);
        }
    }

    // The DTO carries the status as a string for the frontend, so the typed enum
    // is passed alongside it rather than re-parsing the string to switch on.
    private void NotifyClientAsync(ConversationDto dto, ConversationEscalationStatus status, CancellationToken ct)
    {
        try
        {
            var clientEmail = dto.Client.Email;
            if (string.IsNullOrWhiteSpace(clientEmail))
                return;

            var message = status switch
            {
                ConversationEscalationStatus.Escalated => EmailTemplates.ConversationEscalatedToClient(
                    clientEmail, dto.Client.FullName, dto.Agent.FullName, dto.Property.Title, gmailOptions.Value.FrontendBaseUrl),
                ConversationEscalationStatus.Assigned => EmailTemplates.ConversationAssignedToAdmin(
                    clientEmail, dto.Client.FullName, dto.AssignedAdminName ?? "a PIPDC administrator", dto.Property.Title),
                ConversationEscalationStatus.Resolved => EmailTemplates.ConversationEscalationResolved(
                    clientEmail, dto.Client.FullName, dto.ResolvedByName ?? "a PIPDC administrator", dto.Property.Title),
                _ => null
            };

            if (message is not null)
                emailQueue.QueueEmail(logger, message, $"conversation-state:{dto.Id}:{status}", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to queue client email for conversation {ConversationId}.", dto.Id);
        }
    }
}
