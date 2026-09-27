using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.Auth;
using PIPDC.Application.Dashboard;

namespace PIPDC.API.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/dashboard")]
public class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (userId is null)
            return Unauthorized();

        var roles = User.FindAll("role").Select(c => c.Value).ToList();

        if (roles.Contains(Roles.Admin))
        {
            var admin = await dashboardService.GetAdminAsync(userId, roles, ct);
            return admin.ToActionResult();
        }

        if (roles.Contains(Roles.Agent))
        {
            var agent = await dashboardService.GetAgentAsync(userId, roles, ct);
            return agent.ToActionResult();
        }

        var client = await dashboardService.GetClientAsync(userId, ct);
        return client.ToActionResult();
    }
}
