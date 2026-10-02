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
/// Client reports about agents.
///
/// Filing a report requires an authenticated account so the reporter is always
/// identifiable to an administrator. Every role, including an agent, may report;
/// the service rejects self-reports. The admin routes are separately authorized
/// and there is no client-supplied reporter or reviewer id anywhere in this
/// controller, so neither can be impersonated from the request body.
/// </summary>
[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/agent-reports")]
public class AgentReportsController(IAgentReportService reportService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Create([FromBody] CreateAgentReportRequest request, CancellationToken ct)
    {
        var result = await reportService.CreateAsync(CurrentUserId, request, ct);
        return result.ToActionResult();
    }

    /// <summary>The caller's own reports, so they can see the outcome of something they filed.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        // Reuses the admin projection: a reporter is entitled to the status and
        // resolution note of their own report, not to other reports.
        var result = await reportService.GetForReviewAsync(
            new AgentReportQueryParameters { PageSize = 100, ReporterUserId = CurrentUserId }, ct);

        if (result.IsFailure)
            return result.ToActionResult();

        return Ok(result.Value);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] AgentReportQueryParameters queryParams, CancellationToken ct)
    {
        var result = await reportService.GetForReviewAsync(queryParams, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await reportService.GetByIdAsync(id, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPatch("{id:int}/status")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateAgentReportStatusRequest request,
        CancellationToken ct)
    {
        var result = await reportService.UpdateStatusAsync(id, CurrentUserId, request, ct);
        return result.ToActionResult();
    }
}
