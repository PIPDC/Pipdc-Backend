using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.Agents;
using PIPDC.Application.Auth;
using PIPDC.Infrastructure.RateLimiting;
using System.Security.Claims;

namespace PIPDC.API.Controllers;

/// <summary>
/// Client ratings and reviews of agents.
///
/// Reading is public so a review is visible on the agent's public profile.
/// Writing requires an authenticated account because the reviewer is recorded
/// against the JWT subject claim; there is no request field for a reviewer id,
/// so a review cannot be attributed to another account.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/agent-reviews")]
public class AgentReviewsController(IAgentReviewService reviewService) : ControllerBase
{
    private string? CurrentUserId => User.FindFirstValue(JwtRegisteredClaimNames.Sub);

    [HttpGet("{agentId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetForAgent(int agentId, CancellationToken ct)
    {
        var result = await reviewService.GetForAgentAsync(agentId, CurrentUserId, ct);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Submit([FromBody] CreateAgentReviewRequest request, CancellationToken ct)
    {
        var result = await reviewService.SubmitAsync(CurrentUserId!, request, ct);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpGet("{agentId:int}/mine")]
    public async Task<IActionResult> GetMine(int agentId, CancellationToken ct)
    {
        var result = await reviewService.GetMineAsync(agentId, CurrentUserId!, ct);
        return result.ToActionResult();
    }
}
