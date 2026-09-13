using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.Auth;
using PIPDC.Application.Enquiries;
using PIPDC.Infrastructure.Idempotency;
using PIPDC.Infrastructure.RateLimiting;

namespace PIPDC.API.Controllers;

[ApiController]
[Route("api/enquiries")]
public class EnquiriesController(IEnquiryService enquiryService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    private IList<string> CurrentUserRoles => User.FindAll("role").Select(c => c.Value).ToList();

    [Authorize]
    [HttpPost]
    [RequestSizeLimit(100_000)]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    [Idempotent]
    public async Task<IActionResult> Create([FromBody] CreateEnquiryRequest request, CancellationToken ct)
    {
        var result = await enquiryService.CreateAsync(request, CurrentUserId, ct);

        if (result.IsFailure)
            return result.ToActionResult();

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [Authorize(Roles = "Agent,Admin")]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] EnquiryQueryParameters queryParams, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var result = await enquiryService.GetAllAsync(queryParams, userId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] EnquiryQueryParameters queryParams, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var result = await enquiryService.GetMineAsync(userId, queryParams, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet("agents/summary")]
    public async Task<IActionResult> GetAgentSummaries([FromQuery] EnquiryQueryParameters queryParams, CancellationToken ct)
    {
        var result = await enquiryService.GetAgentSummariesAsync(queryParams, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet("agents/{agentId:int}")]
    public async Task<IActionResult> GetByAgent(int agentId, [FromQuery] EnquiryQueryParameters queryParams, CancellationToken ct)
    {
        var result = await enquiryService.GetByAgentAsync(agentId, queryParams, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{id:int}/notify-agent")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    [Idempotent]
    public async Task<IActionResult> NotifyAgent(int id, CancellationToken ct)
    {
        var result = await enquiryService.NotifyAgentAsync(id, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = "Agent,Admin")]
    [HttpGet("property/{propertyId:int}")]
    public async Task<IActionResult> GetByProperty(int propertyId, [FromQuery] EnquiryQueryParameters queryParams, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var result = await enquiryService.GetByPropertyAsync(propertyId, queryParams, userId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = "Agent,Admin")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var result = await enquiryService.GetByIdAsync(id, userId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = "Agent,Admin")]
    [HttpPut("{id:int}")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEnquiryRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var result = await enquiryService.UpdateAsync(id, request, userId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = "Agent,Admin")]
    [HttpDelete("{id:int}")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var result = await enquiryService.DeleteAsync(id, userId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

}
