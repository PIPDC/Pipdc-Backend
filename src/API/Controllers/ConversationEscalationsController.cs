using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.Auth;
using PIPDC.Application.Conversations;
using PIPDC.Infrastructure.RateLimiting;

namespace PIPDC.API.Controllers;

/// <summary>
/// Batch 6: handing a client conversation from its handling agent to PIPDC.
/// <para>
/// These routes sit beside the existing conversation routes rather than beside a
/// new messaging resource, because an escalation is a change of ownership on an
/// existing conversation. History is never copied, moved, or duplicated.
/// </para>
/// <para>
/// Every actor is read from the JWT subject and role claims. No route accepts a
/// user id, an admin id, or an agent id in the body, so a client cannot name an
/// administrator, and an agent can only escalate a conversation they actually own.
/// </para>
/// </summary>
[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/conversations")]
public class ConversationEscalationsController(IConversationEscalationService escalationService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    private IList<string> CurrentUserRoles => User.FindAll("role").Select(c => c.Value).ToList();

    /// <summary>Admin-only queue of escalated, claimed, and resolved conversations.</summary>
    [HttpGet("escalations")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetEscalations([FromQuery] ConversationQueryParameters queryParams, CancellationToken ct)
    {
        var result = await escalationService.GetEscalationsAsync(CurrentUserId, CurrentUserRoles, queryParams, ct);
        return result.ToActionResult();
    }

    /// <summary>Agent hands a conversation to PIPDC, with a reason.</summary>
    [HttpPost("{id:int}/escalate")]
    [Authorize(Roles = Roles.Agent)]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Escalate(int id, [FromBody] EscalateConversationRequest request, CancellationToken ct)
    {
        var result = await escalationService.EscalateAsync(id, request, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    /// <summary>An administrator takes ownership. From here only they may reply.</summary>
    [HttpPost("{id:int}/claim")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Claim(int id, CancellationToken ct)
    {
        var result = await escalationService.ClaimAsync(id, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    /// <summary>The owning administrator closes the case. History is retained.</summary>
    [HttpPost("{id:int}/resolve")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Resolve(int id, CancellationToken ct)
    {
        var result = await escalationService.ResolveAsync(id, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }
}
