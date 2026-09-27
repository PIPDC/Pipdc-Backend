using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.Conversations;
using PIPDC.Infrastructure.Idempotency;
using PIPDC.Infrastructure.RateLimiting;

namespace PIPDC.API.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/conversations/{conversationId:int}/messages")]
public class MessagesController(IMessageService messageService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    private IList<string> CurrentUserRoles => User.FindAll("role").Select(c => c.Value).ToList();

    [HttpGet]
    public async Task<IActionResult> Get(int conversationId, CancellationToken ct)
    {
        var result = await messageService.GetByConversationAsync(conversationId, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    [HttpPost]
    [RequestSizeLimit(100_000)]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    [Idempotent]
    public async Task<IActionResult> Send(int conversationId, [FromBody] SendMessageRequest request, CancellationToken ct)
    {
        var result = await messageService.SendAsync(conversationId, request, CurrentUserId, ct);

        if (result.IsFailure)
            return result.ToActionResult();

        return CreatedAtAction(nameof(Get), new { conversationId }, result.Value);
    }

    [HttpPost("~/api/v{version:apiVersion}/enquiries/{enquiryId:int}/messages")]
    [RequestSizeLimit(100_000)]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    [Idempotent]
    public async Task<IActionResult> SendByEnquiry(int enquiryId, [FromBody] SendMessageRequest request, CancellationToken ct)
    {
        var result = await messageService.SendByEnquiryAsync(enquiryId, request, CurrentUserId, ct);

        if (result.IsFailure)
            return result.ToActionResult();

        return CreatedAtAction(nameof(Get), new { conversationId = result.Value.Conversation.Id }, result.Value);
    }

    [HttpPost("read")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> MarkRead(int conversationId, CancellationToken ct)
    {
        var result = await messageService.MarkReadAsync(conversationId, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }
}
