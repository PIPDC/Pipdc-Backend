using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.Auth;
using PIPDC.Application.Blog;

namespace PIPDC.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/blog")]
public class BlogController(IBlogService blogService) : ControllerBase
{
    private string? CurrentUserId => User.FindFirstValue(JwtRegisteredClaimNames.Sub);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] BlogPostQueryParameters queryParams, CancellationToken ct)
    {
        var result = await blogService.GetAllAsync(queryParams, ct);
        return result.ToActionResult();
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
    {
        var isAdmin = User.IsInRole(Roles.Admin);
        var result = await blogService.GetBySlugAsync(slug, isAdmin, ct);
        return result.ToActionResult();
    }

    [HttpGet("{slug}/related")]
    public async Task<IActionResult> GetRelated(string slug, CancellationToken ct)
    {
        var result = await blogService.GetRelatedAsync(slug, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet("manage")]
    public async Task<IActionResult> GetAllManaged([FromQuery] BlogPostQueryParameters queryParams, CancellationToken ct)
    {
        var result = await blogService.GetAllManagedAsync(queryParams, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBlogPostRequest request, CancellationToken ct)
    {
        var result = await blogService.CreateAsync(request, CurrentUserId, ct);

        if (result.IsFailure)
            return result.ToActionResult();

        return CreatedAtAction(nameof(GetBySlug), new { slug = result.Value.Slug }, result.Value);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBlogPostRequest request, CancellationToken ct)
    {
        var result = await blogService.UpdateAsync(id, request, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await blogService.DeleteAsync(id, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPatch("{id:int}/publish")]
    public async Task<IActionResult> Publish(int id, CancellationToken ct)
    {
        var result = await blogService.PublishAsync(id, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPatch("{id:int}/unpublish")]
    public async Task<IActionResult> Unpublish(int id, CancellationToken ct)
    {
        var result = await blogService.UnpublishAsync(id, ct);
        return result.ToActionResult();
    }
}
