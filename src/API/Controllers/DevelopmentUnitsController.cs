using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PIPDC.API.Extensions;
using PIPDC.Application.Developments;

namespace PIPDC.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/development-projects/{projectId:int}/units")]
public class DevelopmentUnitsController(IDevelopmentUnitService unitService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByProject(int projectId, CancellationToken ct)
    {
        var result = await unitService.GetByProjectAsync(projectId, ct);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create(int projectId, [FromBody] CreateDevelopmentUnitRequest request, CancellationToken ct)
    {
        var result = await unitService.CreateAsync(projectId, request, ct);

        if (result.IsFailure)
            return result.ToActionResult();

        return CreatedAtAction(nameof(GetByProject), new { projectId }, result.Value);
    }

    [HttpPut("{unitId:int}")]
    public async Task<IActionResult> Update(int projectId, int unitId, [FromBody] UpdateDevelopmentUnitRequest request, CancellationToken ct)
    {
        var result = await unitService.UpdateAsync(projectId, unitId, request, ct);
        return result.ToActionResult();
    }

    [HttpDelete("{unitId:int}")]
    public async Task<IActionResult> Delete(int projectId, int unitId, CancellationToken ct)
    {
        var result = await unitService.DeleteAsync(projectId, unitId, ct);
        return result.ToActionResult();
    }
}
