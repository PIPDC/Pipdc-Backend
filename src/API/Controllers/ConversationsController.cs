using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.Conversations;
using PIPDC.Infrastructure.RateLimiting;

namespace PIPDC.API.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/conversations")]
public class ConversationsController(IConversationService conversationService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    private IList<string> CurrentUserRoles => User.FindAll("role").Select(c => c.Value).ToList();

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] ConversationQueryParameters queryParams, CancellationToken ct)
    {
        var result = await conversationService.GetMineAsync(CurrentUserId, CurrentUserRoles, queryParams, ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await conversationService.GetByIdAsync(id, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    [HttpGet("~/api/v{version:apiVersion}/enquiries/{enquiryId:int}/conversation")]
    public async Task<IActionResult> GetStateByEnquiry(int enquiryId, CancellationToken ct)
    {
        var result = await conversationService.GetStateByEnquiryAsync(enquiryId, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    // Opens (or returns) the client's conversation about a property. No enquiry
    // is required, so "Message Agent" works straight from a listing.
    [HttpPost("start")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Start([FromBody] StartConversationRequest request, CancellationToken ct)
    {
        var result = await conversationService.StartAsync(request, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }
}
