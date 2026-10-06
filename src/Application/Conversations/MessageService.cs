using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PIPDC.Application.Data;
using PIPDC.Application.Email;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;

namespace PIPDC.Application.Conversations;

public class MessageService(
    IAppDbContext dbContext,
    IMessageNotifier notifier,
    IEmailQueue emailQueue,
    IOptions<GmailApiSettings> smtpOptions,
    ILogger<MessageService> logger) : IMessageService
{
    public async Task<Result<MessageDto>> SendAsync(int conversationId, SendMessageRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return Result<MessageDto>.Failure(
                Error.Validation("message.emptycontent", "Message content cannot be empty."));

        var conversation = await dbContext.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation is null)
            return Result<MessageDto>.Failure(
                Error.NotFound("conversation.notfound", $"Conversation with id {conversationId} was not found."));

        // Participants may send, plus the administrator who owns an escalated
        // conversation. Once an admin owns the case the original agent is
        // read-only, so two people cannot overwrite each other in the client's
        // thread. The agent is told why, so the block is never silent.
        var sendAllowed = await ConversationAuthorization.AuthorizeSendAsync(dbContext, conversation, currentUserId, currentUserRoles, ct);
        if (sendAllowed.IsFailure)
            return Result<MessageDto>.Failure(sendAllowed.Error);

        var now = DateTime.UtcNow;

        var message = new Message
        {
            ConversationId = conversationId,
            SenderUserId = currentUserId,
            Content = request.Content.Trim(),
            CreatedAt = now
        };

        conversation.LastMessageAt = now;
        conversation.UpdatedAt = now;

        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync(ct);

        var created = await dbContext.Messages
            .Include(m => m.Sender)
            .FirstAsync(m => m.Id == message.Id, ct);

        // Database persistence succeeded — now publish to the conversation group.
        var dto = created.ToDto();
        await PublishNewMessageAsync(conversationId, dto, ct);

        // Send email notification to the other party (best-effort).
        await SendReplyEmailAsync(conversation, currentUserId, request.Content.Trim(), ct);

        return Result<MessageDto>.Success(dto);
    }

    // Sends a message resolved from an Enquiry. If no Conversation exists for the enquiry yet,
    // the Conversation, first Message, and LastMessageAt are created atomically in a single
    // SaveChanges (one implicit database transaction). Opening the messaging UI never calls this.
    public async Task<Result<FirstMessageResultDto>> SendByEnquiryAsync(int enquiryId, SendMessageRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return Result<FirstMessageResultDto>.Failure(
                Error.Validation("message.emptycontent", "Message content cannot be empty."));

        var enquiry = await dbContext.Enquiries
            .Include(e => e.Property)
                .ThenInclude(p => p.Agent)
            .FirstOrDefaultAsync(e => e.Id == enquiryId, ct);

        if (enquiry is null)
            return Result<FirstMessageResultDto>.Failure(
                Error.NotFound("conversation.enquirynotfound", $"Enquiry with id {enquiryId} was not found."));

        // Only the enquiry's client or the managing agent may send. Admins view only.
        if (!await ConversationAuthorization.IsEnquiryParticipantAsync(dbContext, enquiry, currentUserId, ct))
            return Result<FirstMessageResultDto>.Failure(
                Error.Forbidden("message.forbidden", "You cannot send a message for this enquiry."));

        if (enquiry.UserId is null)
            return Result<FirstMessageResultDto>.Failure(
                Error.Conflict("conversation.anonymousclient", "A conversation can only be created for an enquiry submitted by a registered client."));

        if (enquiry.Property.Agent is null)
            return Result<FirstMessageResultDto>.Failure(
                Error.Conflict("conversation.noagent", "This enquiry's property has no assigned agent, so a conversation cannot be created."));

        var content = request.Content.Trim();
        var conversation = await dbContext.Conversations.FirstOrDefaultAsync(c => c.EnquiryId == enquiryId, ct);

        // A conversation that already exists carries escalation state, so the same
        // ownership rule applies whether the client replies from the enquiry screen
        // or from the conversation itself. Without this, the enquiry entry point
        // would be a way around the "administrator owns this case" block.
        if (conversation is not null)
        {
            var sendAllowed = await ConversationAuthorization.AuthorizeSendAsync(dbContext, conversation, currentUserId, currentUserRoles, ct);
            if (sendAllowed.IsFailure)
                return Result<FirstMessageResultDto>.Failure(sendAllowed.Error);
        }

        Message message;
        var attemptedCreate = conversation is null;

        if (conversation is null)
        {
            var now = DateTime.UtcNow;
            conversation = new Conversation
            {
                EnquiryId = enquiryId,
                ClientUserId = enquiry.UserId,
                AgentId = enquiry.Property.AgentId!.Value,
                CreatedAt = now,
                LastMessageAt = now,
                UpdatedAt = now
            };

            message = new Message
            {
                Conversation = conversation,
                SenderUserId = currentUserId,
                Content = content,
                CreatedAt = now
            };

            dbContext.Conversations.Add(conversation);
            dbContext.Messages.Add(message);
        }
        else
        {
            var now = DateTime.UtcNow;
            message = new Message
            {
                ConversationId = conversation.Id,
                SenderUserId = currentUserId,
                Content = content,
                CreatedAt = now
            };

            conversation.LastMessageAt = now;
            conversation.UpdatedAt = now;
            dbContext.Messages.Add(message);
        }

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent first-message request created the conversation first. The unique
            // index on Conversation.EnquiryId guarantees exactly one conversation wins; the
            // failed SaveChanges (and its implicit transaction) rolled back, so discard the
            // local pending entities and re-send the message into the winning conversation.
            if (!attemptedCreate)
                throw;

            dbContext.Messages.Local.Remove(message);
            dbContext.Conversations.Local.Remove(conversation);

            conversation = await dbContext.Conversations.FirstOrDefaultAsync(c => c.EnquiryId == enquiryId, ct);
            if (conversation is null)
                throw;

            var now = DateTime.UtcNow;
            message = new Message
            {
                ConversationId = conversation.Id,
                SenderUserId = currentUserId,
                Content = content,
                CreatedAt = now
            };

            conversation.LastMessageAt = now;
            conversation.UpdatedAt = now;
            dbContext.Messages.Add(message);

            await dbContext.SaveChangesAsync(ct);
        }

        var created = await dbContext.Messages
            .Include(m => m.Sender)
            .FirstAsync(m => m.Id == message.Id, ct);

        var messageDto = created.ToDto();

        var conversationDto = await ConversationProjections.SingleAsync(dbContext, conversation.Id, currentUserId, ct);

        // Persistence succeeded (either the initial save or the concurrency-resolved
        // re-save) — now publish to the winning conversation group exactly once.
        await PublishNewMessageAsync(conversation.Id, messageDto, ct);

        // Send email notification to the other party (best-effort).
        await SendReplyEmailAsync(conversation, currentUserId, content, ct);

        return Result<FirstMessageResultDto>.Success(
            new FirstMessageResultDto(conversationDto, messageDto));
    }

    // SignalR delivery is best-effort: the message is already committed to the
    // database, so a broadcast failure must never turn a successful persistence
    // into a failed REST operation or prompt the client to retry/duplicate it.
    // Only failures from this notification attempt are isolated.
    private async Task PublishNewMessageAsync(int conversationId, MessageDto message, CancellationToken ct)
    {
        try
        {
            await notifier.NotifyNewMessageAsync(conversationId, message, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "SignalR NewMessage broadcast failed for conversation {ConversationId}; the message was already persisted.",
                conversationId);
        }
    }

    private async Task SendReplyEmailAsync(Conversation conversation, string senderUserId, string messageContent, CancellationToken ct)
    {
        try
        {
            var enquiry = await dbContext.Enquiries
                .Include(e => e.Property)
                    .ThenInclude(p => p.Agent)
                    .ThenInclude(a => a!.User)
                .FirstOrDefaultAsync(e => e.Id == conversation.EnquiryId, ct);

            if (enquiry?.Property.Agent?.User is null)
                return;

            var baseUrl = smtpOptions.Value.FrontendBaseUrl;
            var propertyTitle = enquiry.Property.Title;
            var enquiryId = enquiry.Id;
            var preview = messageContent.Length > 120 ? messageContent[..120] + "…" : messageContent;

            bool senderIsClient = senderUserId == conversation.ClientUserId;

            if (senderIsClient)
            {
                // Client sent → email agent
                var agentEmail = enquiry.Property.Agent.User.Email;
                if (string.IsNullOrWhiteSpace(agentEmail))
                    return;

                emailQueue.QueueEmail(
                    logger,
                    EmailTemplates.ClientReplyToAgent(
                        agentEmail,
                        enquiry.Property.Agent.User.FullName,
                        enquiry.FullName,
                        preview,
                        propertyTitle,
                        enquiryId,
                        baseUrl),
                    $"client-reply:{conversation.Id}",
                    ct);
            }
            else
            {
                // Agent sent → email client
                var clientEmail = enquiry.User?.Email ?? enquiry.Email;
                if (string.IsNullOrWhiteSpace(clientEmail))
                    return;

                emailQueue.QueueEmail(
                    logger,
                    EmailTemplates.AgentReplyToClient(
                        clientEmail,
                        enquiry.FullName,
                        enquiry.Property.Agent.User.FullName,
                        preview,
                        propertyTitle,
                        enquiryId,
                        baseUrl),
                    $"agent-reply:{conversation.Id}",
                    ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Failed to send reply email for conversation {ConversationId}.",
                conversation.Id);
        }
    }

    public async Task<Result<IReadOnlyList<MessageDto>>> GetByConversationAsync(int conversationId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var conversation = await dbContext.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation is null)
            return Result<IReadOnlyList<MessageDto>>.Failure(
                Error.NotFound("conversation.notfound", $"Conversation with id {conversationId} was not found."));

        if (!await ConversationAuthorization.CanAccessConversationAsync(dbContext, conversation, currentUserId, currentUserRoles, ct))
            return Result<IReadOnlyList<MessageDto>>.Failure(
                Error.Forbidden("message.forbidden", "You do not have access to this conversation."));

        var messages = await dbContext.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Include(m => m.Sender)
            .ToListAsync(ct);

        return Result<IReadOnlyList<MessageDto>>.Success(messages.Select(m => m.ToDto()).ToList());
    }

    public async Task<Result> MarkReadAsync(int conversationId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var conversation = await dbContext.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation is null)
            return Result.Failure(
                Error.NotFound("conversation.notfound", $"Conversation with id {conversationId} was not found."));

        if (!await ConversationAuthorization.CanAccessConversationAsync(dbContext, conversation, currentUserId, currentUserRoles, ct))
            return Result.Failure(
                Error.Forbidden("message.forbidden", "You do not have access to this conversation."));

        // Mark only messages sent by the other participant as read. A user's own
        // sent messages are never unread for that same user.
        await dbContext.Messages
            .Where(m => m.ConversationId == conversationId
                        && m.SenderUserId != currentUserId
                        && m.ReadAt == null)
            .ExecuteUpdateAsync(m => m.SetProperty(x => x.ReadAt, DateTime.UtcNow), ct);

        return Result.Success();
    }
}
