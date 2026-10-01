using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.Auth;
using PIPDC.Application.Properties;

namespace PIPDC.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/properties")]
public class PropertiesController(IPropertyService propertyService) : ControllerBase
{
    private string? CurrentUserId => User.Identity?.IsAuthenticated == true
        ? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        : null;

    private IList<string> CurrentUserRoles => User.FindAll("role").Select(c => c.Value).ToList();

    /// <summary>
    /// Only an admin sees properties belonging to a suspended agent. Derived from
    /// the caller's own role claim, never from a query parameter.
    /// </summary>
    private bool IncludeSuspendedAgents => CurrentUserRoles.Contains(Roles.Admin);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PropertyQueryParameters queryParams, CancellationToken ct)
    {
        var result = await propertyService.GetAllAsync(queryParams, CurrentUserId, IncludeSuspendedAgents, ct);
        return result.ToActionResult();
    }

    [HttpGet("featured")]
    public async Task<IActionResult> GetFeatured(CancellationToken ct)
    {
        var result = await propertyService.GetFeaturedAsync(CurrentUserId, IncludeSuspendedAgents, ct);
        return result.ToActionResult();
    }

    [HttpGet("slug/{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
    {
        var result = await propertyService.GetBySlugAsync(slug, CurrentUserId, IncludeSuspendedAgents, ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await propertyService.GetByIdAsync(id, CurrentUserId, IncludeSuspendedAgents, ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:int}/similar")]
    public async Task<IActionResult> GetSimilar(int id, CancellationToken ct)
    {
        var result = await propertyService.GetSimilarAsync(id, CurrentUserId, IncludeSuspendedAgents, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = "Agent,Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePropertyRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

        var result = await propertyService.CreateAsync(request, userId, CurrentUserRoles, ct);

        if (result.IsFailure)
            return result.ToActionResult();

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [Authorize(Roles = "Agent,Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePropertyRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var result = await propertyService.UpdateAsync(id, request, userId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/featured")]
    public async Task<IActionResult> SetFeatured(int id, [FromBody] UpdateFeaturedRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var result = await propertyService.SetFeaturedAsync(id, request.Featured, userId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = "Agent,Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var result = await propertyService.DeleteAsync(id, userId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = "Agent,Admin")]
    [HttpDelete("{id:int}/images/{publicId}")]
    public async Task<IActionResult> RemoveImage(int id, string publicId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var result = await propertyService.RemoveImageAsync(id, publicId, userId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = "Agent,Admin")]
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeStatusRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var result = await propertyService.ChangeStatusAsync(id, request.Status, userId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = "Agent,Admin")]
    [HttpPatch("{id:int}/listing-type")]
    public async Task<IActionResult> ChangeListingType(int id, [FromBody] ChangeListingTypeRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var result = await propertyService.ChangeListingTypeAsync(id, request.ListingType, userId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/agent")]
    public async Task<IActionResult> AssignAgent(int id, [FromBody] AssignAgentRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var result = await propertyService.AssignAgentAsync(id, request.AgentId, userId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }
}
