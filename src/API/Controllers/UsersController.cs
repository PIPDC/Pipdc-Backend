using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PIPDC.API.Extensions;
using PIPDC.Application.Auth;
using PIPDC.Application.Users;

namespace PIPDC.API.Controllers;

[Authorize(Roles = Roles.Admin)]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/users")]
public class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] UserQueryParameters queryParams, CancellationToken ct)
    {
        var result = await userService.GetAllAsync(queryParams, ct);
        return result.ToActionResult();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var result = await userService.GetByIdAsync(id, ct);
        return result.ToActionResult();
    }

    [HttpPost("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(string id, CancellationToken ct)
    {
        var result = await userService.DeactivateAsync(id, ct);
        return result.ToActionResult();
    }

    [HttpPost("{id}/activate")]
    public async Task<IActionResult> Activate(string id, CancellationToken ct)
    {
        var result = await userService.ActivateAsync(id, ct);
        return result.ToActionResult();
    }
}
