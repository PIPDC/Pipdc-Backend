using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.Developments;
using PIPDC.Infrastructure.RateLimiting;

namespace PIPDC.API.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/development-tracking")]
public class DevelopmentTrackingController(IDevelopmentTrackingService trackingService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    [HttpGet]
    public async Task<IActionResult> GetTracked([FromQuery] DevelopmentProjectQueryParameters queryParams, CancellationToken ct)
    {
        var result = await trackingService.GetTrackedAsync(CurrentUserId, queryParams, ct);
        return result.ToActionResult();
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Track([FromBody] TrackProjectRequest request, CancellationToken ct)
    {
        var result = await trackingService.TrackAsync(CurrentUserId, request.ProjectId, request.UnitId, ct);
        return result.ToActionResult();
    }

    [HttpDelete("{projectId:int}")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> StopTracking(int projectId, CancellationToken ct)
    {
        var result = await trackingService.StopTrackingAsync(CurrentUserId, projectId, ct);
        return result.ToActionResult();
    }
}
