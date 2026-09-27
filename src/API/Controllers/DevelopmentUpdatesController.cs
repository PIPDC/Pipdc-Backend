using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PIPDC.API.Extensions;
using PIPDC.Application.Developments;

namespace PIPDC.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/development-projects/{projectId:int}/updates")]
public class DevelopmentUpdatesController(IDevelopmentUpdateService updateService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByProject(int projectId, CancellationToken ct)
    {
        var result = await updateService.GetByProjectAsync(projectId, ct);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create(int projectId, [FromBody] CreateDevelopmentUpdateRequest request, CancellationToken ct)
    {
        var result = await updateService.CreateAsync(projectId, request, ct);

        if (result.IsFailure)
            return result.ToActionResult();

        return CreatedAtAction(nameof(GetByProject), new { projectId }, result.Value);
    }

    [HttpPut("{updateId:int}")]
    public async Task<IActionResult> Update(int projectId, int updateId, [FromBody] UpdateDevelopmentUpdateRequest request, CancellationToken ct)
    {
        var result = await updateService.UpdateAsync(projectId, updateId, request, ct);
        return result.ToActionResult();
    }

    [HttpDelete("{updateId:int}")]
    public async Task<IActionResult> Delete(int projectId, int updateId, CancellationToken ct)
    {
        var result = await updateService.DeleteAsync(projectId, updateId, ct);
        return result.ToActionResult();
    }
}
