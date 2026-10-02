using Microsoft.EntityFrameworkCore;
using PIPDC.Application.Auth;
using PIPDC.Application.Common;
using PIPDC.Application.Data;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;

namespace PIPDC.Application.Conversations;

public class ConversationService(IAppDbContext dbContext) : IConversationService
{
    // Read-only state for an enquiry's messaging screen. Never creates a Conversation:
    // a Conversation only comes into existence when the first message is sent.
    public async Task<Result<EnquiryConversationStateDto>> GetStateByEnquiryAsync(int enquiryId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var enquiry = await dbContext.Enquiries
            .Include(e => e.Property)
                .ThenInclude(p => p.Agent)
                .ThenInclude(a => a!.User)
            .FirstOrDefaultAsync(e => e.Id == enquiryId, ct);

        if (enquiry is null)
            return Result<EnquiryConversationStateDto>.Failure(
                Error.NotFound("conversation.enquirynotfound", $"Enquiry with id {enquiryId} was not found."));

        if (!await ConversationAuthorization.CanAccessEnquiryAsync(dbContext, enquiry, currentUserId, currentUserRoles, ct))
            return Result<EnquiryConversationStateDto>.Failure(
                Error.Forbidden("conversation.forbidden", "You do not have access to this enquiry."));

        ConversationDto? conversationDto = null;

        var existingId = await dbContext.Conversations
            .Where(c => c.EnquiryId == enquiryId)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);

        if (existingId != 0)
            conversationDto = await ConversationProjections.SingleAsync(dbContext, existingId, currentUserId, ct);

        var agent = enquiry.Property.Agent is not null
            ? new ConversationAgentDto(
                enquiry.Property.AgentId,
                enquiry.Property.Agent.UserId,
                enquiry.Property.Agent.User.FullName,
                enquiry.Property.Agent.AgencyName,
                enquiry.Property.Agent.PhotoUrl)
            : new ConversationAgentDto(0, null, string.Empty, string.Empty, null);

        return Result<EnquiryConversationStateDto>.Success(new EnquiryConversationStateDto(
            enquiryId,
            conversationDto,
            new ConversationClientDto(enquiry.UserId ?? string.Empty, enquiry.FullName, enquiry.Email),
            agent,
            new ConversationPropertyDto(enquiry.PropertyId, enquiry.Property.Title, enquiry.Property.Slug)));
    }

    public async Task<Result<PaginatedResult<ConversationDto>>> GetMineAsync(string currentUserId, IList<string> currentUserRoles, ConversationQueryParameters q, CancellationToken ct)
    {
        IQueryable<Conversation> query = dbContext.Conversations;

        if (!currentUserRoles.Contains(Roles.Admin))
        {
            var agentIds = await dbContext.Agents
                .Where(a => a.UserId == currentUserId)
                .Select(a => a.Id)
                .ToListAsync(ct);

            query = query.Where(c => c.ClientUserId == currentUserId || agentIds.Contains(c.AgentId));
        }

        var totalCount = await query.CountAsync(ct);

        query = query.OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt);

        var items = await ConversationProjections.Project(query, currentUserId)
            .Skip((q.PageNumber - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync(ct);

        return Result<PaginatedResult<ConversationDto>>.Success(
            PaginatedResult<ConversationDto>.Create(items.Select(ConversationProjections.ToDto).ToList(), totalCount, q.PageNumber, q.PageSize));
    }

    public async Task<Result<ConversationDto>> GetByIdAsync(int id, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var conversation = await dbContext.Conversations.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (conversation is null)
            return Result<ConversationDto>.Failure(
                Error.NotFound("conversation.notfound", $"Conversation with id {id} was not found."));

        if (!await ConversationAuthorization.CanAccessConversationAsync(dbContext, conversation, currentUserId, currentUserRoles, ct))
            return Result<ConversationDto>.Failure(
                Error.Forbidden("conversation.forbidden", "You do not have access to this conversation."));

        return Result<ConversationDto>.Success(await ConversationProjections.SingleAsync(dbContext, id, currentUserId, ct));
    }
}
