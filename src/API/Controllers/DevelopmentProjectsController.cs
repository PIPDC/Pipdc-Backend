using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PIPDC.API.Extensions;
using PIPDC.Application.Developments;

namespace PIPDC.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/development-projects")]
public class DevelopmentProjectsController(IDevelopmentProjectService projectService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DevelopmentProjectQueryParameters queryParams, CancellationToken ct)
    {
        var result = await projectService.GetAllAsync(queryParams, ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await projectService.GetByIdAsync(id, ct);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDevelopmentProjectRequest request, CancellationToken ct)
    {
        var result = await projectService.CreateAsync(request, ct);

        if (result.IsFailure)
            return result.ToActionResult();

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDevelopmentProjectRequest request, CancellationToken ct)
    {
        var result = await projectService.UpdateAsync(id, request, ct);
        return result.ToActionResult();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await projectService.DeleteAsync(id, ct);
        return result.ToActionResult();
    }

    [HttpPut("{id:int}/featured")]
    public async Task<IActionResult> SetFeatured(int id, [FromBody] UpdateFeaturedDevelopmentRequest request, CancellationToken ct)
    {
        var result = await projectService.UpdateFeaturedAsync(id, request.Featured, ct);
        return result.ToActionResult();
    }
}
