using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PIPDC.Application.Services;
using PIPDC.Infrastructure.RateLimiting;

namespace PIPDC.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
public class ImagesController : ControllerBase
{
    private readonly IImageService _imageService;

    private static readonly HashSet<string> AllowedTypes =
    [
        "image/jpeg", "image/png", "image/webp", "image/gif"
    ];

    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

    public ImagesController(IImageService imageService)
    {
        _imageService = imageService;
    }

    [Authorize]
    [HttpPost("upload")]
    [EnableRateLimiting(RateLimitPolicies.Uploads)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile file,
        [FromQuery] string folder = "general",
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded." });

        if (!AllowedTypes.Contains(file.ContentType))
            return BadRequest(new { message = $"File type '{file.ContentType}' is not allowed. Use JPEG, PNG, WebP, or GIF." });

        if (file.Length > MaxFileSize)
            return BadRequest(new { message = "File size must be 10 MB or less." });

        var result = await _imageService.UploadAsync(file.OpenReadStream(), file.FileName, folder, ct);

        return Ok(new { url = result.Url, publicId = result.PublicId });
    }

    [Authorize]
    [HttpDelete("{publicId}")]
    public async Task<IActionResult> Delete(string publicId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(publicId))
            return BadRequest(new { message = "PublicId is required." });

        await _imageService.DeleteAsync(publicId, ct);

        return Ok(new { message = "Image deleted." });
    }
}
