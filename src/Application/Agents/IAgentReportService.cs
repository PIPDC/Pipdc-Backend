using PIPDC.Application.Common;
using PIPDC.Domain.Common;

namespace PIPDC.Application.Agents;

public interface IAgentReportService
{
    /// <summary>
    /// Files a report against a visible agent. The reporter is always the
    /// authenticated caller, taken from the JWT subject claim, so a report can
    /// never be filed in someone else's name.
    /// </summary>
    Task<Result<AgentReportDto>> CreateAsync(
        string reporterUserId,
        CreateAgentReportRequest request,
        CancellationToken ct);

    /// <summary>Admin triage queue.</summary>
    Task<Result<PaginatedResult<AgentReportDto>>> GetForReviewAsync(
        AgentReportQueryParameters q,
        CancellationToken ct);

    Task<Result<AgentReportDto>> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Moves a report through its lifecycle. The reviewing administrator is
    /// taken from the JWT subject claim.
    /// </summary>
    Task<Result<AgentReportDto>> UpdateStatusAsync(
        int reportId,
        string adminUserId,
        UpdateAgentReportStatusRequest request,
        CancellationToken ct);
}
