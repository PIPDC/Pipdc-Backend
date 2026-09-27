using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PIPDC.API.Extensions;
using PIPDC.Application.Developments;

namespace PIPDC.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/development-projects")]
public class DevelopmentProjectsPublicController(IDevelopmentProjectPublicService publicService) : ControllerBase
{
    [HttpGet("browse")]
    public async Task<IActionResult> Browse([FromQuery] DevelopmentProjectQueryParameters queryParams, CancellationToken ct)
    {
        var result = await publicService.GetPublicAllAsync(queryParams, ct);
        return result.ToActionResult();
    }

    [HttpGet("browse/{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
    {
        var result = await publicService.GetPublicBySlugAsync(slug, ct);
        return result.ToActionResult();
    }

    [HttpGet("browse/{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await publicService.GetPublicByIdAsync(id, ct);
        return result.ToActionResult();
    }
}
