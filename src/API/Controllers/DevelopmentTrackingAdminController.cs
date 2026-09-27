using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PIPDC.API.Extensions;
using PIPDC.Application.Developments;

namespace PIPDC.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/development-tracking")]
public class DevelopmentTrackingAdminController(IDevelopmentTrackingService trackingService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DevelopmentTrackingQueryParameters queryParams, CancellationToken ct)
    {
        var result = await trackingService.AdminGetAllAsync(queryParams, ct);
        return result.ToActionResult();
    }

    [HttpGet("project/{projectId:int}")]
    public async Task<IActionResult> GetByProject(int projectId, CancellationToken ct)
    {
        var result = await trackingService.AdminGetByProjectAsync(projectId, ct);
        return result.ToActionResult();
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetByUser(string userId, CancellationToken ct)
    {
        var result = await trackingService.AdminGetByUserAsync(userId, ct);
        return result.ToActionResult();
    }

    [HttpDelete("{trackingId:int}")]
    public async Task<IActionResult> RemoveTracking(int trackingId, CancellationToken ct)
    {
        var result = await trackingService.AdminRemoveTrackingAsync(trackingId, ct);
        return result.ToActionResult();
    }

    [HttpPut("{trackingId:int}/status")]
    public async Task<IActionResult> UpdateStatus(int trackingId, [FromBody] UpdateTrackingStatusRequest request, CancellationToken ct)
    {
        var result = await trackingService.AdminUpdateTrackingStatusAsync(trackingId, request.Status, ct);
        return result.ToActionResult();
    }
}
