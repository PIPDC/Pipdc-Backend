using PIPDC.Application.Common;
using PIPDC.Domain.Common;

namespace PIPDC.Application.Agents;

public interface IAgentService
{
  Task<Result<PaginatedResult<AgentDto>>> GetAllAsync(
      AgentQueryParameters queryParams,
      bool includeSuspended,
      CancellationToken ct);

  /// <summary>
  /// Reads a single agent. A suspended agent is reported as not found unless
  /// includeSuspended is set, which the API layer derives from the caller's role.
  /// </summary>
  Task<Result<AgentDto>> GetByIdAsync(int id, bool includeSuspended, CancellationToken ct);
    Task<Result<AgentDto>> GetMyProfileAsync(string userId, CancellationToken ct);
    Task<Result<AgentDto>> CreateAsync(CreateAgentRequest request, CancellationToken ct);
    Task<Result<AgentDto>> UpdateAsync(int id, UpdateAgentRequest request, CancellationToken ct);
    Task<Result> DeleteAsync(int id, CancellationToken ct);
    Task<Result<AgentDto>> ToggleVerificationAsync(int agentId, CancellationToken ct);
    Task<Result<AgentSummaryDto>> GetSummaryAsync(int agentId, bool includeSuspended, CancellationToken ct);
}
