using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.Agents;
using PIPDC.Application.Auth;

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

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await agentService.DeleteAsync(id, ct);
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
}
