using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.AiChat;

namespace PIPDC.API.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/ai-chat")]
public class AiChatController(IAiChatService aiChatService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    [HttpGet("session")]
    public async Task<IActionResult> GetSession(CancellationToken ct)
    {
        var result = await aiChatService.GetSessionAsync(CurrentUserId, ct);
        return result.ToActionResult();
    }

    [HttpPost("session/messages")]
    public async Task<IActionResult> SendMessage([FromBody] SendAiMessageRequest request, CancellationToken ct)
    {
        var result = await aiChatService.SendAsync(CurrentUserId, request.Content, ct);
        return result.ToActionResult();
    }

    [HttpDelete("session")]
    public async Task<IActionResult> DeleteSession(CancellationToken ct)
    {
        var result = await aiChatService.DeleteAsync(CurrentUserId, ct);
        return result.ToActionResult();
    }
}