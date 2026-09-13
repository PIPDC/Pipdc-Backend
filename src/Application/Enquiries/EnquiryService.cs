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

namespace PIPDC.Application.Enquiries;

public class EnquiryService(
    IAppDbContext dbContext,
    UserManager<AppUser> userManager,
    IEmailService emailService,
    IOptions<GmailApiSettings> smtpOptions,
    ILogger<EnquiryService> logger) : IEnquiryService
{
    public async Task<Result<PaginatedResult<EnquiryDto>>> GetAllAsync(EnquiryQueryParameters q, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        IQueryable<Enquiry> query = dbContext.Enquiries;

        if (!currentUserRoles.Contains(Roles.Admin))
        {
            var propertyIds = await dbContext.Properties
                .Where(p => p.Agent != null && p.Agent.UserId == currentUserId)
                .Select(p => p.Id)
                .ToListAsync(ct);

            query = query.Where(e => propertyIds.Contains(e.PropertyId));
        }

        query = ApplyFilters(query, q);
        var totalCount = await query.CountAsync(ct);

        query = ApplySorting(query, q);

        var items = await Project(query)
            .Skip((q.PageNumber - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync(ct);

        return Result<PaginatedResult<EnquiryDto>>.Success(
            PaginatedResult<EnquiryDto>.Create(items, totalCount, q.PageNumber, q.PageSize));
    }

    public async Task<Result<PaginatedResult<EnquiryDto>>> GetMineAsync(string userId, EnquiryQueryParameters q, CancellationToken ct)
    {
        IQueryable<Enquiry> query = dbContext.Enquiries.Where(e => e.UserId == userId);

        query = ApplyFilters(query, q);
        var totalCount = await query.CountAsync(ct);

        query = ApplySorting(query, q);

        var items = await Project(query)
            .Skip((q.PageNumber - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync(ct);

        return Result<PaginatedResult<EnquiryDto>>.Success(
            PaginatedResult<EnquiryDto>.Create(items, totalCount, q.PageNumber, q.PageSize));
    }

    public async Task<Result<PaginatedResult<EnquiryDto>>> GetByPropertyAsync(int propertyId, EnquiryQueryParameters q, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (!await dbContext.Properties.AnyAsync(p => p.Id == propertyId, ct))
            return Result<PaginatedResult<EnquiryDto>>.Failure(
                Error.NotFound("property.notfound", $"Property with id {propertyId} was not found."));

        if (!currentUserRoles.Contains(Roles.Admin))
        {
            var ownsProperty = await dbContext.Properties.AnyAsync(
                p => p.Id == propertyId && p.Agent != null && p.Agent.UserId == currentUserId, ct);
            if (!ownsProperty)
                return Result<PaginatedResult<EnquiryDto>>.Failure(
                    Error.Forbidden("enquiry.forbidden", "You cannot view enquiries for a property you do not own."));
        }

        IQueryable<Enquiry> query = dbContext.Enquiries.Where(e => e.PropertyId == propertyId);
        query = ApplyFilters(query, q);
        var totalCount = await query.CountAsync(ct);
        query = ApplySorting(query, q);

        var items = await Project(query)
            .Skip((q.PageNumber - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync(ct);

        return Result<PaginatedResult<EnquiryDto>>.Success(
            PaginatedResult<EnquiryDto>.Create(items, totalCount, q.PageNumber, q.PageSize));
    }

    public async Task<Result<EnquiryDto>> GetByIdAsync(int id, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var enquiry = await dbContext.Enquiries
            .Include(e => e.Property)
                .ThenInclude(p => p.Agent)
                .ThenInclude(a => a!.User)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (enquiry is null)
            return Result<EnquiryDto>.Failure(
                Error.NotFound("enquiry.notfound", $"Enquiry with id {id} was not found."));

        var access = await CanManageEnquiryAsync(enquiry, currentUserId, currentUserRoles, ct);
        if (access.IsFailure)
            return Result<EnquiryDto>.Failure(access.Error);

        if (!currentUserRoles.Contains(Roles.Admin) && enquiry.AgentReadAt is null)
        {
            enquiry.AgentReadAt = DateTime.UtcNow;
            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Best-effort read marker; a concurrent update is fine.
            }
        }

        return Result<EnquiryDto>.Success(enquiry.ToDto());
    }

    public async Task<Result<EnquiryDto>> CreateAsync(CreateEnquiryRequest request, string currentUserId, CancellationToken ct)
    {
        if (!await dbContext.Properties.AnyAsync(p => p.Id == request.PropertyId, ct))
            return Result<EnquiryDto>.Failure(
                Error.Validation("enquiry.invalidproperty", $"Property with id {request.PropertyId} does not exist."));

        var user = await userManager.FindByIdAsync(currentUserId);
        if (user is null)
            return Result<EnquiryDto>.Failure(
                Error.Unauthorized("enquiry.unauthorized", "The authenticated account no longer exists."));

        // F.1: A client can only have one open enquiry per property. Reuse any
        // existing open enquiry so double-submits (even with a fresh idempotency
        // key) return the same enquiry instead of creating duplicates. A new enquiry
        // becomes possible once the previous one is resolved.
        var openEnquiry = await dbContext.Enquiries
            .Include(e => e.Property)
                .ThenInclude(p => p.Agent)
                .ThenInclude(a => a!.User)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(
                e => e.UserId == currentUserId
                  && e.PropertyId == request.PropertyId
                  && e.Status != EnquiryStatus.Resolved,
                ct);

        if (openEnquiry is not null)
            return Result<EnquiryDto>.Success(openEnquiry.ToDto());

        var enquiry = new Enquiry
        {
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email ?? string.Empty,
            Phone = user.PhoneNumber,
            Message = request.Message,
            Status = EnquiryStatus.Pending,
            PropertyId = request.PropertyId,
            UserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Enquiries.Add(enquiry);
        await dbContext.SaveChangesAsync(ct);

        var created = await dbContext.Enquiries
            .Include(e => e.Property)
                .ThenInclude(p => p.Agent)
                .ThenInclude(a => a!.User)
            .FirstAsync(e => e.Id == enquiry.Id, ct);

        // Email the assigned agent about the new enquiry (best-effort).
        if (created.Property.Agent?.User?.Email is { Length: > 0 } agentEmail)
        {
            try
            {
                var baseUrl = smtpOptions.Value.FrontendBaseUrl;
                await emailService.SendAsync(
                    EmailTemplates.NewEnquiryToAgent(
                        agentEmail,
                        created.Property.Agent.User.FullName,
                        created.FullName,
                        created.Message,
                        created.Property.Title,
                        created.Id,
                        baseUrl),
                    ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to send new-enquiry email for enquiry {EnquiryId}.", created.Id);
            }
        }

        return Result<EnquiryDto>.Success(created.ToDto());
    }

    public async Task<Result<EnquiryDto>> UpdateAsync(int id, UpdateEnquiryRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var enquiry = await dbContext.Enquiries
            .Include(e => e.Property)
                .ThenInclude(p => p.Agent)
                .ThenInclude(a => a!.User)
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (enquiry is null)
            return Result<EnquiryDto>.Failure(
                Error.NotFound("enquiry.notfound", $"Enquiry with id {id} was not found."));

        var access = await CanManageEnquiryAsync(enquiry, currentUserId, currentUserRoles, ct);
        if (access.IsFailure)
            return Result<EnquiryDto>.Failure(access.Error);

        if (!Enum.TryParse<EnquiryStatus>(request.Status, true, out var status))
            return Result<EnquiryDto>.Failure(
                Error.Validation("enquiry.invalidstatus", $"'{request.Status}' is not a valid enquiry status."));

        enquiry.FullName = request.FullName;
        enquiry.Email = request.Email;
        enquiry.Phone = request.Phone;
        enquiry.Message = request.Message;
        enquiry.Status = status;
        enquiry.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<EnquiryDto>.Failure(Error.Concurrency());
        }

        // Send status-change emails (best-effort, never fail the operation).
        await SendStatusChangeEmailsAsync(enquiry, status, ct);

        return Result<EnquiryDto>.Success(enquiry.ToDto());
    }

    public async Task<Result> DeleteAsync(int id, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var enquiry = await dbContext.Enquiries.FindAsync([id], ct);

        if (enquiry is null)
            return Result.Failure(
                Error.NotFound("enquiry.notfound", $"Enquiry with id {id} was not found."));

        var access = await CanManageEnquiryAsync(enquiry, currentUserId, currentUserRoles, ct);
        if (access.IsFailure)
            return Result.Failure(access.Error);

        dbContext.Enquiries.Remove(enquiry);

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Concurrency());
        }

        return Result.Success();
    }

    public async Task<Result<PaginatedResult<AgentEnquirySummaryDto>>> GetAgentSummariesAsync(EnquiryQueryParameters q, CancellationToken ct)
    {
        var aggregates = await dbContext.Enquiries
            .Where(e => e.Property.AgentId != null)
            .GroupBy(e => e.Property.AgentId!.Value)
            .Select(g => new
            {
                AgentId = g.Key,
                TotalEnquiries = g.Count(),
                UnreadEnquiries = g.Count(e => e.AgentReadAt == null),
                LatestEnquiryAt = g.Max(e => e.CreatedAt)
            })
            .ToListAsync(ct);

        var agentIds = aggregates.Select(a => a.AgentId).ToList();

        var agents = await dbContext.Agents
            .Where(a => agentIds.Contains(a.Id))
            .Include(a => a.User)
            .ToListAsync(ct);
        var agentById = agents.ToDictionary(a => a.Id);

        List<AgentEnquirySummaryDto> items = aggregates
            .Select(a => new AgentEnquirySummaryDto(
                a.AgentId,
                agentById.TryGetValue(a.AgentId, out var agent) ? agent.User?.FullName ?? string.Empty : string.Empty,
                a.TotalEnquiries,
                a.UnreadEnquiries,
                a.LatestEnquiryAt))
            .ToList();

        items = q.SortBy?.ToLower() switch
        {
            "unread" => q.SortDescending ? items.OrderByDescending(s => s.UnreadEnquiries).ThenBy(s => s.AgentId).ToList()
                                         : items.OrderBy(s => s.UnreadEnquiries).ThenBy(s => s.AgentId).ToList(),
            "name" => q.SortDescending ? items.OrderByDescending(s => s.AgentName).ThenBy(s => s.AgentId).ToList()
                                       : items.OrderBy(s => s.AgentName).ThenBy(s => s.AgentId).ToList(),
            _ => q.SortDescending ? items.OrderByDescending(s => s.LatestEnquiryAt).ThenBy(s => s.AgentId).ToList()
                                  : items.OrderBy(s => s.LatestEnquiryAt).ThenBy(s => s.AgentId).ToList()
        };

        var totalCount = items.Count;

        var pagedItems = items
            .Skip((q.PageNumber - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToList();

        return Result<PaginatedResult<AgentEnquirySummaryDto>>.Success(
            PaginatedResult<AgentEnquirySummaryDto>.Create(pagedItems, totalCount, q.PageNumber, q.PageSize));
    }

    public async Task<Result<PaginatedResult<EnquiryDto>>> GetByAgentAsync(int agentId, EnquiryQueryParameters q, CancellationToken ct)
    {
        IQueryable<Enquiry> query = dbContext.Enquiries.Where(e => e.Property.AgentId == agentId);

        query = ApplyFilters(query, q);
        var totalCount = await query.CountAsync(ct);

        query = ApplySorting(query, q);

        var items = await Project(query)
            .Skip((q.PageNumber - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync(ct);

        return Result<PaginatedResult<EnquiryDto>>.Success(
            PaginatedResult<EnquiryDto>.Create(items, totalCount, q.PageNumber, q.PageSize));
    }

    public async Task<Result<AgentNotifyResultDto>> NotifyAgentAsync(int id, CancellationToken ct)
    {
        var enquiry = await dbContext.Enquiries
            .Include(e => e.Property)
                .ThenInclude(p => p.Agent)
                .ThenInclude(a => a!.User)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (enquiry is null)
            return Result<AgentNotifyResultDto>.Failure(
                Error.NotFound("enquiry.notfound", $"Enquiry with id {id} was not found."));

        // Email the agent a reminder (best-effort).
        if (enquiry.Property.Agent?.User?.Email is { Length: > 0 } agentEmail)
        {
            try
            {
                var baseUrl = smtpOptions.Value.FrontendBaseUrl;
                await emailService.SendAsync(
                    EmailTemplates.AdminNotifyToAgent(
                        agentEmail,
                        enquiry.Property.Agent.User.FullName,
                        enquiry.FullName,
                        enquiry.Property.Title,
                        enquiry.Id,
                        baseUrl),
                    ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to send admin-notify email for enquiry {EnquiryId}.", enquiry.Id);
            }
        }

        return Result<AgentNotifyResultDto>.Success(new AgentNotifyResultDto(
            enquiry.Id,
            enquiry.Status.ToString(),
            enquiry.FullName,
            enquiry.Email,
            enquiry.Phone,
            enquiry.Message,
            enquiry.Property.AgentId,
            enquiry.Property.Agent?.User?.FullName ?? string.Empty,
            enquiry.Property.Agent?.User?.Email ?? string.Empty,
            enquiry.PropertyId,
            enquiry.Property.Title,
            enquiry.Property.Slug,
            enquiry.AgentReadAt));
    }

    private static IQueryable<Enquiry> ApplyFilters(IQueryable<Enquiry> query, EnquiryQueryParameters q)
    {
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var keyword = q.Keyword.ToLower();
            query = query.Where(e => e.FullName.ToLower().Contains(keyword)
                                  || e.Email.ToLower().Contains(keyword)
                                  || e.Message.ToLower().Contains(keyword));
        }

        if (Enum.TryParse<EnquiryStatus>(q.Status, true, out var status))
            query = query.Where(e => e.Status == status);

        if (q.PropertyId.HasValue)
            query = query.Where(e => e.PropertyId == q.PropertyId.Value);

        return query;
    }

    private static IQueryable<Enquiry> ApplySorting(IQueryable<Enquiry> query, EnquiryQueryParameters q) =>
        q.SortBy?.ToLower() switch
        {
            "status" => q.SortDescending ? query.OrderByDescending(e => e.Status)
                                         : query.OrderBy(e => e.Status),
            _ => q.SortDescending ? query.OrderByDescending(e => e.CreatedAt)
                                  : query.OrderBy(e => e.CreatedAt)
        };

    private static IQueryable<EnquiryDto> Project(IQueryable<Enquiry> query) =>
        query.Select(e => new EnquiryDto(
            e.Id,
            e.FullName,
            e.Email,
            e.Phone,
            e.Message,
            e.Status.ToString(),
            e.PropertyId,
            e.Property.Title,
            e.Property.Slug,
            e.UserId,
            e.Property.AgentId,
            e.Property.Agent != null ? e.Property.Agent.User.FullName : string.Empty,
            e.AgentReadAt,
            e.AgentReadAt != null,
            e.CreatedAt,
            e.UpdatedAt));

    private async Task<Result> CanManageEnquiryAsync(Enquiry enquiry, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (currentUserRoles.Contains(Roles.Admin))
            return Result.Success();

        var ownsProperty = await dbContext.Properties.AnyAsync(
            p => p.Id == enquiry.PropertyId && p.Agent != null && p.Agent.UserId == currentUserId, ct);

        return ownsProperty
            ? Result.Success()
            : Result.Failure(
                Error.Forbidden("enquiry.forbidden", "You cannot manage an enquiry for a property you do not own."));
    }

    private async Task SendStatusChangeEmailsAsync(Enquiry enquiry, EnquiryStatus newStatus, CancellationToken ct)
    {
        var baseUrl = smtpOptions.Value.FrontendBaseUrl;
        var clientEmail = enquiry.User?.Email ?? enquiry.Email;
        var clientName = enquiry.FullName;
        var agentEmail = enquiry.Property.Agent?.User?.Email;
        var agentName = enquiry.Property.Agent?.User?.FullName ?? string.Empty;
        var propertyTitle = enquiry.Property.Title;

        try
        {
            if (newStatus == EnquiryStatus.ViewingScheduled)
            {
                // Email client
                if (!string.IsNullOrWhiteSpace(clientEmail))
                    await emailService.SendAsync(
                        EmailTemplates.ViewingScheduledToClient(clientEmail, clientName, propertyTitle, enquiry.Id, baseUrl), ct);

                // Email agent
                if (!string.IsNullOrWhiteSpace(agentEmail))
                    await emailService.SendAsync(
                        EmailTemplates.ViewingScheduledToAgent(agentEmail, agentName, clientName, propertyTitle, enquiry.Id, baseUrl), ct);
            }
            else if (newStatus == EnquiryStatus.Resolved)
            {
                // Email client
                if (!string.IsNullOrWhiteSpace(clientEmail))
                    await emailService.SendAsync(
                        EmailTemplates.EnquiryResolvedToClient(clientEmail, clientName, propertyTitle, enquiry.Id, baseUrl), ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to send status-change email for enquiry {EnquiryId} (status: {Status}).", enquiry.Id, newStatus);
        }
    }
}
