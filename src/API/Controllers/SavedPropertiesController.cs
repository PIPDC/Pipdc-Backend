using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.SavedProperties;
using PIPDC.Infrastructure.RateLimiting;

namespace PIPDC.API.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/saved-properties")]
public class SavedPropertiesController(ISavedPropertyService savedPropertyService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    [HttpGet]
    public async Task<IActionResult> GetSaved([FromQuery] SavedPropertyQueryParameters queryParams, CancellationToken ct)
    {
        var result = await savedPropertyService.GetSavedAsync(CurrentUserId, queryParams, ct);
        return result.ToActionResult();
    }

    [HttpGet("ids")]
    public async Task<IActionResult> GetSavedIds(CancellationToken ct)
    {
        var result = await savedPropertyService.GetSavedIdsAsync(CurrentUserId, ct);
        return result.ToActionResult();
    }

    [HttpPost("{propertyId:int}")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Save(int propertyId, CancellationToken ct)
    {
        var result = await savedPropertyService.SaveAsync(CurrentUserId, propertyId, ct);
        return result.ToActionResult();
    }

    [HttpDelete("{propertyId:int}")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Unsave(int propertyId, CancellationToken ct)
    {
        var result = await savedPropertyService.UnsaveAsync(CurrentUserId, propertyId, ct);
        return result.ToActionResult();
    }
}
