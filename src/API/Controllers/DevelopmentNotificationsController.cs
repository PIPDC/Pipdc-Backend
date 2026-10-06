using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.Developments;

namespace PIPDC.API.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/development-notifications")]
public class DevelopmentNotificationsController(IDevelopmentProjectService projectService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        return (await projectService.GetNotificationsAsync(userId, ct)).ToActionResult();
    }

    [HttpPost("{id:int}/mark-read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        return (await projectService.MarkNotificationReadAsync(id, userId, ct)).ToActionResult();
    }
}
