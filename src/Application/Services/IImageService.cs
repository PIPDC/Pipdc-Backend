namespace PIPDC.Application.Services;

public interface IImageService
{
    Task<ImageUploadResult> UploadAsync(Stream file, string fileName, string folder, CancellationToken ct = default);
    Task DeleteAsync(string publicId, CancellationToken ct = default);
}

public record ImageUploadResult(string Url, string PublicId);
