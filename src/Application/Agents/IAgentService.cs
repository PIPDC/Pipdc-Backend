using System.ComponentModel.DataAnnotations;
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
    /// <summary>
    /// Revokes an agent registration, retaining the row.
    /// </summary>
    /// <remarks>
    /// Retained rather than deleted so the agent's properties, enquiries, reviews
    /// and reports keep pointing at a real person, and so an upheld appeal can
    /// restore the same registration.
    /// </remarks>
    Task<Result<AgentRemovalResult>> RemoveAsync(
        int id,
        string adminUserId,
        RemoveAgentRequest? request,
        CancellationToken ct);

    Task<Result<AgentDto>> ToggleVerificationAsync(int agentId, CancellationToken ct);
    Task<Result<AgentSummaryDto>> GetSummaryAsync(int agentId, bool includeSuspended, CancellationToken ct);

    /// <summary>
    /// Suspends an agent and records the administrative reason and the
    /// administrator who decided it. Idempotent: re-suspending an already
    /// suspended agent is a no-op so a double-click cannot overwrite the
    /// original reason or timestamp.
    /// </summary>
    Task<Result<AgentSummaryDto>> SuspendAsync(
        int agentId,
        string adminUserId,
        SuspendAgentRequest request,
        CancellationToken ct);

    /// <summary>
    /// Lifts a suspension and clears the moderation state, so a reinstated agent
    /// reappears in the public directory and listing routes immediately.
    /// </summary>
    Task<Result<AgentSummaryDto>> ReinstateAsync(int agentId, CancellationToken ct);
}

public record SuspendAgentRequest(
    [Required(ErrorMessage = "Give a reason for the suspension.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "The reason must be between 10 and 500 characters.")]
    string Reason);
