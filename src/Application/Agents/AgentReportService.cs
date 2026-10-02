using Microsoft.EntityFrameworkCore;
using PIPDC.Application.Common;
using PIPDC.Application.Data;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Agents;

public class AgentReportService(IAppDbContext dbContext) : IAgentReportService
{
    // Same shape as the guard in AgentApplicationService, so a genuine database
    // fault is never reported to the client as a duplicate report.
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true;

    public async Task<Result<AgentReportDto>> CreateAsync(
        string reporterUserId,
        CreateAgentReportRequest request,
        CancellationToken ct)
    {
        // Read through the public visibility rule. A suspended agent has no public
        // profile, so reporting one would confirm the id exists.
        var target = await dbContext.Agents
            .VisibleAgents()
            .FirstOrDefaultAsync(a => a.Id == request.AgentId, ct);

        if (target is null)
            return Result<AgentReportDto>.Failure(
                Error.NotFound("report.agentnotfound", $"Agent with id {request.AgentId} was not found."));

        if (!Enum.IsDefined(request.Reason))
            return Result<AgentReportDto>.Failure(
                Error.Validation("report.invalidreason", "Select a valid report reason."));

        var description = request.Description?.Trim() ?? string.Empty;
        if (description.Length is < 20 or > 2000)
            return Result<AgentReportDto>.Failure(
                Error.Validation("report.invaliddescription", "The description must be between 20 and 2000 characters."));

        // An agent cannot report themselves. A client account is expected to have
        // no agent profile, but a dual-role account must not be able to file
        // reports that would be triaged against its own moderation record.
        var reporterAgentId = await dbContext.Agents
            .Where(a => a.UserId == reporterUserId)
            .Select(a => (int?)a.Id)
            .FirstOrDefaultAsync(ct);

        if (reporterAgentId == target.Id)
            return Result<AgentReportDto>.Failure(
                Error.Validation("report.selfreport", "You cannot report your own agent profile."));

        // Duplicate protection: one unresolved report per reporter per agent,
        // backed by a partial unique index so a race cannot create a second one.
        var alreadyOpen = await dbContext.AgentReports.AnyAsync(
            r => r.AgentId == target.Id
              && r.ReporterUserId == reporterUserId
              && (r.Status == AgentReportStatus.Open || r.Status == AgentReportStatus.UnderReview),
            ct);

        if (alreadyOpen)
            return Result<AgentReportDto>.Failure(
                Error.Conflict("report.duplicate", "You already have an open report for this agent. An administrator will review it."));

        var report = new AgentReport
        {
            AgentId = target.Id,
            ReporterUserId = reporterUserId,
            Reason = request.Reason,
            Description = description,
            Status = AgentReportStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.AgentReports.Add(report);

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // The partial unique index is the authority. Two rapid submissions from
            // the same client both pass the pre-check above, so translate the
            // database rejection into the same readable conflict. Only a unique
            // violation is mapped here; any other database failure is a real fault
            // and is left to propagate to the global handler.
            return Result<AgentReportDto>.Failure(
                Error.Conflict("report.duplicate", "You already have an open report for this agent. An administrator will review it."));
        }

        var created = await LoadReportAsync(report.Id, ct);
        if (created is null)
            return Result<AgentReportDto>.Failure(
                Error.Failure("report.createfailed", "The report could not be read back after saving."));

        return Result<AgentReportDto>.Success(created.ToDto());
    }

    public async Task<Result<PaginatedResult<AgentReportDto>>> GetForReviewAsync(
        AgentReportQueryParameters q,
        CancellationToken ct)
    {
        var query = dbContext.AgentReports
            .Include(r => r.Agent)
                .ThenInclude(a => a.User)
            .Include(r => r.Reporter)
            .AsQueryable();

        if (q.Status.HasValue)
            query = query.Where(r => r.Status == q.Status.Value);

        if (q.AgentId.HasValue)
            query = query.Where(r => r.AgentId == q.AgentId.Value);

        if (!string.IsNullOrWhiteSpace(q.ReporterUserId))
            query = query.Where(r => r.ReporterUserId == q.ReporterUserId);

        var totalCount = await query.CountAsync(ct);

        // Open work first, then oldest first, so a backlog drains in submission order.
        // The entities are materialized before mapping: ToDto reads Reporter and
        // Agent.User, and projecting inside Select() runs in the database where
        // those navigations are not available, which threw a NullReferenceException.
        var page = await query
            .OrderBy(r => r.Status == AgentReportStatus.Open ? 0 : 1)
            .ThenBy(r => r.Status == AgentReportStatus.UnderReview ? 0 : 1)
            .ThenBy(r => r.CreatedAt)
            .Skip((q.PageNumber - 1) * q.PageSize)
            .Take(q.PageSize)
            .ToListAsync(ct);

        var items = page.Select(r => r.ToDto()).ToList();

        return Result<PaginatedResult<AgentReportDto>>.Success(
            PaginatedResult<AgentReportDto>.Create(items, totalCount, q.PageNumber, q.PageSize));
    }

    public async Task<Result<AgentReportDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var report = await LoadReportAsync(id, ct);

        if (report is null)
            return Result<AgentReportDto>.Failure(
                Error.NotFound("report.notfound", $"Report with id {id} was not found."));

        return Result<AgentReportDto>.Success(report.ToDto());
    }

    public async Task<Result<AgentReportDto>> UpdateStatusAsync(
        int reportId,
        string adminUserId,
        UpdateAgentReportStatusRequest request,
        CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Status))
            return Result<AgentReportDto>.Failure(
                Error.Validation("report.invalidstatus", "Select a valid status."));

        var report = await dbContext.AgentReports.FirstOrDefaultAsync(r => r.Id == reportId, ct);

        if (report is null)
            return Result<AgentReportDto>.Failure(
                Error.NotFound("report.notfound", $"Report with id {reportId} was not found."));

        var note = request.ResolutionNote?.Trim();

        // A closing decision is only auditable if it says what was decided. This
        // is the administrative resolution the audit trail depends on, so it is
        // required for both terminal states rather than merely recommended.
        if (request.Status is AgentReportStatus.Resolved or AgentReportStatus.Dismissed
            && string.IsNullOrWhiteSpace(note))
        {
            return Result<AgentReportDto>.Failure(
                Error.Validation("report.resolutionnoterequired",
                    "A resolution note is required when resolving or dismissing a report."));
        }

        if (note is { Length: > 1000 })
            return Result<AgentReportDto>.Failure(
                Error.Validation("report.resolutionnotetoolong", "The resolution note cannot exceed 1000 characters."));

        var isClosing = request.Status is AgentReportStatus.Resolved or AgentReportStatus.Dismissed;

        report.Status = request.Status;
        report.ResolutionNote = note;
        report.UpdatedAt = DateTime.UtcNow;

        if (isClosing)
        {
            report.ReviewedAt = DateTime.UtcNow;
            report.ReviewedByAdminId = adminUserId;
        }
        else
        {
            // Reopening or re-queuing clears the previous closure so the history
            // never shows a stale reviewer against an unresolved report.
            report.ReviewedAt = null;
            report.ReviewedByAdminId = null;
        }

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<AgentReportDto>.Failure(Error.Concurrency());
        }

        var updated = await LoadReportAsync(reportId, ct);
        if (updated is null)
            return Result<AgentReportDto>.Failure(
                Error.Failure("report.updatefailed", "The report could not be read back after saving."));

        return Result<AgentReportDto>.Success(updated.ToDto());
    }

    // ToDto reads report.Agent.User.FullName, so the nested user has to be
    // included. Loading only Agent left User null and threw on read-back.
    private Task<AgentReport?> LoadReportAsync(int id, CancellationToken ct) =>
        dbContext.AgentReports
            .Include(r => r.Agent)
                .ThenInclude(a => a.User)
            .Include(r => r.Reporter)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);
}
