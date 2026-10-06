using System.ComponentModel.DataAnnotations;

namespace PIPDC.Application.Developments;

// ── Response DTOs ──────────────────────────────────────────────────────────

public record DevelopmentProjectDto(
    int Id,
    string Name,
    string Slug,
    string Description,
    string Location,
    int? LocationRefId,
    string? Developer,
    string Status,
    DateTime? ExpectedCompletionDate,
    int ProgressPercentage,
    bool Featured,
    IReadOnlyList<DevelopmentProjectImageDto> Images,
    int UnitCount,
    int UpdateCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // Surfaced on the list as well as the detail page: an admin has to be able to
    // tell at a glance which project owns a listing, and the edit form needs the
    // current id so saving an unrelated field does not unlink it.
    int? PropertyId = null,
    string? PropertyTitle = null);

public record DevelopmentProjectImageDto(
    int Id,
    string Url,
    string PublicId,
    bool IsCover,
    int DisplayOrder);

public record DevelopmentUnitDto(
    int Id,
    string UnitIdentifier,
    string UnitType,
    string Status,
    decimal? Price,
    string Currency,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // ── Listing descriptor ──────────────────────────────────────────────
    string ListingType,
    string PropertyType,
    string? Period,
    int? Bedrooms,
    int? Bathrooms,
    double? Size,
    string SizeUnit,
    int? YearBuilt,
    IReadOnlyList<string> Amenities,
    // ── Promotion state ─────────────────────────────────────────────────
    int? PropertyId,
    string? PropertyStatus,
    bool IsListable,
    IReadOnlyList<string> MissingDetails);

public record DevelopmentUpdateDto(
    int Id,
    string Title,
    string Description,
    int? ProgressPercentage,
    DateTime UpdateDate,
    IReadOnlyList<string> ImageUrls,
    IReadOnlyList<string> ImagePublicIds,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record DevelopmentTrackingDto(
    int Id,
    int DevelopmentProjectId,
    string DevelopmentProjectName,
    int? DevelopmentUnitId,
    string? DevelopmentUnitIdentifier,
    string Status,
    DateTime TrackedAt);

public record AdminDevelopmentTrackingDto(
    int Id,
    string UserId,
    string UserFullName,
    string UserEmail,
    int DevelopmentProjectId,
    string DevelopmentProjectName,
    int? DevelopmentUnitId,
    string? DevelopmentUnitIdentifier,
    string Status,
    DateTime TrackedAt);

public record DevelopmentProjectDetailDto(
    int Id,
    string Name,
    string Slug,
    string Description,
    string Location,
    int? LocationRefId,
    string? Developer,
    string Status,
    DateTime? ExpectedCompletionDate,
    int ProgressPercentage,
    bool Featured,
    IReadOnlyList<DevelopmentProjectImageDto> Images,
    int UnitCount,
    int UpdateCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<DevelopmentUnitDto> Units,
    IReadOnlyList<DevelopmentUpdateDto> Updates,
    // The linked listing and whether the admin still has to promote it. Drives
    // the explicit "List Property" action on the admin project page.
    int? PropertyId = null,
    string? PropertyTitle = null,
    string? PropertySlug = null,
    string? PropertyStatus = null);

// ── Request DTOs ───────────────────────────────────────────────────────────

public record CreateDevelopmentProjectRequest(
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(4000)] string Description,
    [MaxLength(200)] string? Slug,
    [Required, MaxLength(500)] string Location,
    int? LocationRefId,
    [MaxLength(200)] string? Developer,
    string? Status,
    DateTime? ExpectedCompletionDate,
    [Range(0, 100)] int? ProgressPercentage,
    bool Featured = false,
    int? PropertyId = null,
    List<CreateDevelopmentProjectImageRequest>? Images = null);

public record UpdateDevelopmentProjectRequest(
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(4000)] string Description,
    [MaxLength(200)] string? Slug,
    [Required, MaxLength(500)] string Location,
    int? LocationRefId,
    [MaxLength(200)] string? Developer,
    [Required] string Status,
    DateTime? ExpectedCompletionDate,
    [Range(0, 100)] int? ProgressPercentage,
    bool Featured = false,
    // The listing this project represents. The admin sets this so that the
    // listing prompt has something to act on when the project completes.
    int? PropertyId = null,
    List<CreateDevelopmentProjectImageRequest>? Images = null);

public record CreateDevelopmentProjectImageRequest(
    [Required, MaxLength(500)] string Url,
    [Required, MaxLength(200)] string PublicId,
    bool IsCover = false,
    int DisplayOrder = 0);

public record UpdateFeaturedDevelopmentRequest(bool Featured);

// ── Unit Request DTOs ──────────────────────────────────────────────────────

public record CreateDevelopmentUnitRequest(
    [Required, MaxLength(50)] string UnitIdentifier,
    [Required, MaxLength(100)] string UnitType,
    string? Status,
    decimal? Price,
    [MaxLength(10)] string? Currency,
    [MaxLength(2000)] string? Description,
    // ── Listing descriptor ──────────────────────────────────────────────
    // A unit is the individual house, so it carries the fields a listing needs.
    string? ListingType,
    string? PropertyType,
    [MaxLength(50)] string? Period,
    int? Bedrooms,
    int? Bathrooms,
    double? Size,
    [MaxLength(10)] string? SizeUnit,
    int? YearBuilt,
    IReadOnlyList<string>? Amenities);

public record UpdateDevelopmentUnitRequest(
    [Required, MaxLength(50)] string UnitIdentifier,
    [Required, MaxLength(100)] string UnitType,
    [Required] string Status,
    decimal? Price,
    [MaxLength(10)] string? Currency,
    [MaxLength(2000)] string? Description,
    // ── Listing descriptor ──────────────────────────────────────────────
    string? ListingType,
    string? PropertyType,
    [MaxLength(50)] string? Period,
    int? Bedrooms,
    int? Bathrooms,
    double? Size,
    [MaxLength(10)] string? SizeUnit,
    int? YearBuilt,
    IReadOnlyList<string>? Amenities);

// ── Update Request DTOs ────────────────────────────────────────────────────

public record CreateDevelopmentUpdateRequest(
    [Required, MaxLength(200)] string Title,
    [Required, MaxLength(4000)] string Description,
    [Range(0, 100)] int? ProgressPercentage,
    DateTime? UpdateDate,
    List<string>? ImageUrls = null,
    List<string>? ImagePublicIds = null);

public record UpdateDevelopmentUpdateRequest(
    [Required, MaxLength(200)] string Title,
    [Required, MaxLength(4000)] string Description,
    [Range(0, 100)] int? ProgressPercentage,
    DateTime? UpdateDate,
    List<string>? ImageUrls = null,
    List<string>? ImagePublicIds = null);

// ── Tracking Request DTOs ──────────────────────────────────────────────────

public record TrackProjectRequest(int ProjectId, int? UnitId);

public record UpdateTrackingStatusRequest([Required] string Status);

public record NotificationDto(
    int Id,
    string Type,
    string Title,
    string Message,
    string? Link,
    bool IsRead,
    DateTime CreatedAt);

