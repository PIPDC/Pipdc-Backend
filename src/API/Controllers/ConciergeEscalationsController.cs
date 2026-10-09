using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.AiChat;
using PIPDC.Application.Auth;
using PIPDC.Infrastructure.RateLimiting;

namespace PIPDC.API.Controllers;

/// <summary>
/// Concierge escalation queue: chats the AI property assistant handed to PIPDC
/// because it could not resolve them itself (no matching property, a request to
/// speak to a human, or an organizational request a real person must act on).
/// <para>
/// Each case hangs off its <c>AiChatSession</c> transcript, so the administrator
/// reads the full chat here and then reaches the client outside the chat; there is
/// no in-chat takeover. The concierge keeps answering a client who keeps chatting.
/// </para>
/// </summary>
[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/concierge-escalations")]
public class ConciergeEscalationsController(IConciergeEscalationService escalationService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    private IList<string> CurrentUserRoles => User.FindAll("role").Select(c => c.Value).ToList();

    /// <summary>Admin-only queue of concierge escalations, unresolved first.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetEscalations([FromQuery] ConciergeEscalationQueryParameters queryParams, CancellationToken ct)
    {
        var result = await escalationService.GetEscalationsAsync(CurrentUserId, CurrentUserRoles, queryParams, ct);
        return result.ToActionResult();
    }

    /// <summary>Full case: the escalation row plus the complete chat transcript.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await escalationService.GetByIdAsync(id, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    /// <summary>An administrator takes ownership of the case.</summary>
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