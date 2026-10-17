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

    // Opens a conversation about a property without requiring an enquiry. The
    // thread is identified by (client, agent, property), so calling this again
    // returns the same conversation instead of stacking duplicates.
    public async Task<Result<StartConversationResultDto>> StartAsync(StartConversationRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var property = await dbContext.Properties
            .VisibleProperties()
            .Include(p => p.Agent)
            .FirstOrDefaultAsync(p => p.Id == request.PropertyId, ct);

        if (property is null)
            return Result<StartConversationResultDto>.Failure(
                Error.NotFound("conversation.propertynotfound", $"Property with id {request.PropertyId} was not found."));

        if (property.AgentId is null)
            return Result<StartConversationResultDto>.Failure(
                Error.Conflict("conversation.noagent", "This property has no assigned agent to message."));

        // An agent cannot open a conversation with themselves; the client is the
        // only party who starts a thread from a listing.
        if (property.Agent is not null && property.Agent.UserId == currentUserId)
            return Result<StartConversationResultDto>.Failure(
                Error.Validation("conversation.selfmessage", "You cannot start a conversation with yourself."));

        var agentId = property.AgentId.Value;

        var existingId = await FindConversationIdAsync(currentUserId, agentId, property.Id, ct);
        if (existingId != 0)
            return Result<StartConversationResultDto>.Success(new StartConversationResultDto(existingId));

        var now = DateTime.UtcNow;
        var conversation = new Conversation
        {
            PropertyId = property.Id,
            ClientUserId = currentUserId,
            AgentId = agentId,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Conversations.Add(conversation);

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent request inserted the same (client, agent, property)
            // thread first and the unique index rejected ours; return the winner.
            var winnerId = await FindConversationIdAsync(currentUserId, agentId, property.Id, ct);
            if (winnerId == 0)
                throw;

            return Result<StartConversationResultDto>.Success(new StartConversationResultDto(winnerId));
        }

        return Result<StartConversationResultDto>.Success(new StartConversationResultDto(conversation.Id));
    }

    private Task<int> FindConversationIdAsync(string clientUserId, int agentId, int propertyId, CancellationToken ct) =>
        dbContext.Conversations
            .Where(c => c.ClientUserId == clientUserId && c.AgentId == agentId && c.PropertyId == propertyId)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);
}
