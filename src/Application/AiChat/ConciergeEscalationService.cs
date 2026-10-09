using Microsoft.EntityFrameworkCore;
using PIPDC.Application.Auth;
using PIPDC.Application.Common;
using PIPDC.Application.Data;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.AiChat;

public class ConciergeEscalationService(IAppDbContext dbContext) : IConciergeEscalationService
{
    public async Task<Result<PaginatedResult<ConciergeEscalationDto>>> GetEscalationsAsync(
        string currentUserId, IList<string> currentUserRoles, ConciergeEscalationQueryParameters q, CancellationToken ct)
    {
        if (!currentUserRoles.Contains(Roles.Admin))
            return Result<PaginatedResult<ConciergeEscalationDto>>.Failure(
                Error.Forbidden("concierge.admironly", "Only an administrator can view the concierge escalation queue."));

        // Unclaimed work first, then claimed cases, then resolved history so the
        // queue doubles as an audit trail. Resolved rows are only hidden when the
        // status filter says so.
        IQueryable<ConciergeEscalation> query = dbContext.ConciergeEscalations;

        var statusFilter = ParseStatus(q.Status);
        if (statusFilter is not null)
            query = query.Where(e => e.EscalationStatus == statusFilter);

        var totalCount = await query.CountAsync(ct);

        // Ordered on the entity query, before projection: the projection carries the
        // status as a string for the frontend, so the typed comparison has to happen
        // while the enum is still available.
        var ordered = query
            .OrderBy(e => e.EscalationStatus == ConciergeEscalationStatus.Resolved ? 1 : 0)
            .ThenBy(e => e.EscalationStatus == ConciergeEscalationStatus.Assigned ? 1 : 0)
            .ThenByDescending(e => e.EscalatedAt);

        var records = await ConciergeEscalationProjections.Project(ordered)
            .Skip((q.PageNumber - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync(ct);

        return Result<PaginatedResult<ConciergeEscalationDto>>.Success(
            PaginatedResult<ConciergeEscalationDto>.Create(
                records.Select(ConciergeEscalationProjections.ToDto).ToList(),
                totalCount, q.PageNumber, q.PageSize));
    }

    public async Task<Result<ConciergeEscalationDetailDto>> GetByIdAsync(
        int id, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (!currentUserRoles.Contains(Roles.Admin))
            return Result<ConciergeEscalationDetailDto>.Failure(
                Error.Forbidden("concierge.admironly", "Only an administrator can view a concierge escalation."));

        if (!await dbContext.ConciergeEscalations.AnyAsync(e => e.Id == id, ct))
            return Result<ConciergeEscalationDetailDto>.Failure(
                Error.NotFound("concierge.notfound", $"Concierge escalation with id {id} was not found."));

        var dto = await ConciergeEscalationProjections.SingleAsync(dbContext, id, ct);

        var messagesJson = await dbContext.ConciergeEscalations
            .Where(e => e.Id == id)
            .Select(e => e.AiChatSession.MessagesJson)
            .FirstAsync(ct);

        return Result<ConciergeEscalationDetailDto>.Success(
            new ConciergeEscalationDetailDto(dto, ConciergeEscalationProjections.Transcript(messagesJson)));
    }

    public async Task<Result<ConciergeEscalationDto>> ClaimAsync(
        int id, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (!currentUserRoles.Contains(Roles.Admin))
            return Result<ConciergeEscalationDto>.Failure(
                Error.Forbidden("concierge.admironly", "Only an administrator can take ownership of a concierge escalation."));

        var escalation = await dbContext.ConciergeEscalations.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (escalation is null)
            return Result<ConciergeEscalationDto>.Failure(
                Error.NotFound("concierge.notfound", $"Concierge escalation with id {id} was not found."));

        if (escalation.EscalationStatus is not ConciergeEscalationStatus.Escalated)
            return Result<ConciergeEscalationDto>.Failure(
                Error.Conflict("concierge.notawaitingadmin", "This case is not waiting for an administrator."));

        var now = DateTime.UtcNow;
        escalation.EscalationStatus = ConciergeEscalationStatus.Assigned;
        escalation.AssignedAdminId = currentUserId;
        escalation.AssignedAt = now;
        escalation.UpdatedAt = now;

        await dbContext.SaveChangesAsync(ct);

        var dto = await ConciergeEscalationProjections.SingleAsync(dbContext, id, ct);
        return Result<ConciergeEscalationDto>.Success(dto);
    }

    public async Task<Result<ConciergeEscalationDto>> ResolveAsync(
        int id, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (!currentUserRoles.Contains(Roles.Admin))
            return Result<ConciergeEscalationDto>.Failure(
                Error.Forbidden("concierge.admironly", "Only an administrator can resolve a concierge escalation."));

        var escalation = await dbContext.ConciergeEscalations.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (escalation is null)
            return Result<ConciergeEscalationDto>.Failure(
                Error.NotFound("concierge.notfound", $"Concierge escalation with id {id} was not found."));

        if (escalation.EscalationStatus is not ConciergeEscalationStatus.Assigned)
            return Result<ConciergeEscalationDto>.Failure(
                Error.Conflict("concierge.notassigned", "Take ownership of this case before resolving it."));

        if (!string.Equals(escalation.AssignedAdminId, currentUserId, StringComparison.Ordinal))
            return Result<ConciergeEscalationDto>.Failure(
                Error.Forbidden("concierge.notyourcase", "Another administrator owns this case."));

        var now = DateTime.UtcNow;
        escalation.EscalationStatus = ConciergeEscalationStatus.Resolved;
        escalation.ResolvedByUserId = currentUserId;
        escalation.ResolvedAt = now;
        escalation.UpdatedAt = now;

        await dbContext.SaveChangesAsync(ct);

        var dto = await ConciergeEscalationProjections.SingleAsync(dbContext, id, ct);
        return Result<ConciergeEscalationDto>.Success(dto);
    }

    private static ConciergeEscalationStatus? ParseStatus(string? status) =>
        Enum.TryParse<ConciergeEscalationStatus>(status, ignoreCase: true, out var parsed) ? parsed : null;
}