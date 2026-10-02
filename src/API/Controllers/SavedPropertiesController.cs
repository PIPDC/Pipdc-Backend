using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.Auth;
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

    // Saved bookmarks mirror the public listing surface, so a suspended agent's
    // inventory must disappear from them too. Derived from the caller's own role
    // rather than a query parameter, as everywhere else in the API.
    private bool IncludeSuspended => User.IsInRole(Roles.Admin);

    [HttpGet]
    public async Task<IActionResult> GetSaved([FromQuery] SavedPropertyQueryParameters queryParams, CancellationToken ct)
    {
        var result = await savedPropertyService.GetSavedAsync(CurrentUserId, IncludeSuspended, queryParams, ct);
        return result.ToActionResult();
    }

    [HttpGet("ids")]
    public async Task<IActionResult> GetSavedIds(CancellationToken ct)
    {
        var result = await savedPropertyService.GetSavedIdsAsync(CurrentUserId, IncludeSuspended, ct);
        return result.ToActionResult();
    }

    [HttpPost("{propertyId:int}")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Save(int propertyId, CancellationToken ct)
    {
        var result = await savedPropertyService.SaveAsync(CurrentUserId, propertyId, IncludeSuspended, ct);
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
