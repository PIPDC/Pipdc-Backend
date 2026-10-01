using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.Agents;
using PIPDC.Infrastructure.RateLimiting;

namespace PIPDC.API.Controllers;

/// <summary>
/// Authenticated endpoint for a signed-in user to apply to become a PIPDC agent.
///
/// There is no endpoint here that promotes a user to agent, and no user id in
/// any request body. The applicant is always the bearer of the access token.
/// </summary>
[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/agent-applications")]
public class AgentApplicationsController(IAgentApplicationService applicationService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Submit([FromBody] AgentApplicationRequest request, CancellationToken ct)
    {
        var result = await applicationService.SubmitAsync(CurrentUserId, request, ct);
        return result.ToActionResult();
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var result = await applicationService.GetMineAsync(CurrentUserId, ct);
        return result.ToActionResult();
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken ct)
    {
        var result = await applicationService.GetCurrentAsync(CurrentUserId, ct);
        return result.ToActionResult();
    }
}
