using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.Agents;
using PIPDC.Application.Auth;
using PIPDC.Infrastructure.RateLimiting;

namespace PIPDC.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/agents")]
public class AgentsController(IAgentService agentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] AgentQueryParameters queryParams, CancellationToken ct)
    {
        // Suspended agents are hidden from this public directory. Admins keep
        // full visibility, derived from the caller's own claim rather than a
        // query parameter a public caller could set.
        var includeSuspended = User.IsInRole(Roles.Admin);
        var result = await agentService.GetAllAsync(queryParams, includeSuspended, ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        // A suspended agent's public profile is not found. The same 404 an
        // unknown id returns, so the directory does not confirm that a suspended
        // agent exists.
        var result = await agentService.GetByIdAsync(id, User.IsInRole(Roles.Admin), ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = Roles.Agent)]
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var result = await agentService.GetMyProfileAsync(userId, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAgentRequest request, CancellationToken ct)
    {
        var result = await agentService.CreateAsync(request, ct);

        if (result.IsFailure)
            return result.ToActionResult();

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAgentRequest request, CancellationToken ct)
    {
        var result = await agentService.UpdateAsync(id, request, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Revokes an agent registration. This is deliberately not a plain delete: the
    /// row is retained and marked removed, the Agent role is dropped, the approval
    /// is marked revoked rather than left reading "Approved", and the applicant is
    /// emailed.
    /// </summary>
    /// <remarks>
    /// The request body is required even though HTTP DELETE bodies are awkward,
    /// because a removal without a stated reason is exactly the action that was
    /// invisible and indefensible last time.
    /// </remarks>
    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:int}")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Remove(
        int id,
        [FromBody] RemoveAgentRequest? request,
        CancellationToken ct)
    {
        // The deciding administrator comes from the JWT subject claim, not the body.
        var adminUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var result = await agentService.RemoveAsync(id, adminUserId, request, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:int}/verify")]
    public async Task<IActionResult> ToggleVerification(int id, CancellationToken ct)
    {
        var result = await agentService.ToggleVerificationAsync(id, ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:int}/summary")]
    public async Task<IActionResult> GetSummary(int id, CancellationToken ct)
    {
        var result = await agentService.GetSummaryAsync(id, User.IsInRole(Roles.Admin), ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Suspends an agent. The agent keeps its profile and existing data but
    /// disappears from the public directory, their listings stop resolving for
    /// non-admins, and their own property mutations are refused.
    /// </summary>
    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{id:int}/suspension")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Suspend(int id, [FromBody] SuspendAgentRequest request, CancellationToken ct)
    {
        // The deciding administrator comes from the JWT subject claim, not the body.
        var adminUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var result = await agentService.SuspendAsync(id, adminUserId, request, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:int}/suspension")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Reinstate(int id, CancellationToken ct)
    {
        var result = await agentService.ReinstateAsync(id, ct);
        return result.ToActionResult();
    }
}
