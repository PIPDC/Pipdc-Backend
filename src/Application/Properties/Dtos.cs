using System.ComponentModel.DataAnnotations;

namespace PIPDC.Application.Properties;

public record PropertyDto(
    int Id,
    string Title,
    string Slug,
    string Description,
    decimal Price,
    string Currency,
    string? Period,
    string Status,
    string Type,
    string PropertyType,
    string ListingType,
    int? Bedrooms,
    int? Bathrooms,
    double? Size,
    string SizeUnit,
    double? LotSize,
    int? YearBuilt,
    string Address,
    string City,
    string? Area,
    string State,
    double? Latitude,
    double? Longitude,
    int? LocationId,
    IReadOnlyList<string> Images,
    string? CoverImage,
    IReadOnlyList<string> Amenities,
    bool Featured,
    int? AgentId,
    string? AgentName,
    string? AgentPhoto,
    bool IsSaved,
    int EnquiryCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreatePropertyRequest(
    [Required, MaxLength(200)] string Title,
    [Required, MaxLength(4000)] string Description,
    [MaxLength(200)] string? Slug,
    [Range(0, 1_000_000_000_000)] decimal Price,
    [MaxLength(10)] string? Currency,
    [MaxLength(50)] string? Period,
    string? Type,
    string? PropertyType,
    string? Status,
    string? ListingType,
    [Range(0, 100)] int? Bedrooms,
    [Range(0, 100)] int? Bathrooms,
    [Range(0, 1_000_000)] double? Size,
    [MaxLength(10)] string? SizeUnit,
    [Range(0, 1_000_000_000)] double? LotSize,
    [Range(1800, 2100)] int? YearBuilt,
    [Required, MaxLength(500)] string Address,
    [Required, MaxLength(100)] string State,
    [Required, MaxLength(100)] string City,
    [MaxLength(100)] string? Area,
    [Range(-90, 90)] double? Latitude,
    [Range(-180, 180)] double? Longitude,
    int? LocationId,
    List<string>? Amenities,
    List<string>? Images,
    bool Featured = false,
    int? AgentId = null);

public record UpdatePropertyRequest(
    [Required, MaxLength(200)] string Title,
    [Required, MaxLength(4000)] string Description,
    [MaxLength(200)] string? Slug,
    [Range(0, 1_000_000_000_000)] decimal Price,
    [MaxLength(10)] string? Currency,
    [MaxLength(50)] string? Period,
    string? Type,
    string? PropertyType,
    string? Status,
    string? ListingType,
    [Range(0, 100)] int? Bedrooms,
    [Range(0, 100)] int? Bathrooms,
    [Range(0, 1_000_000)] double? Size,
    [MaxLength(10)] string? SizeUnit,
    [Range(0, 1_000_000_000)] double? LotSize,
    [Range(1800, 2100)] int? YearBuilt,
    [Required, MaxLength(500)] string Address,
    [Required, MaxLength(100)] string State,
    [Required, MaxLength(100)] string City,
    [MaxLength(100)] string? Area,
    [Range(-90, 90)] double? Latitude,
    [Range(-180, 180)] double? Longitude,
    int? LocationId,
    List<string>? Amenities,
    List<string>? Images,
    bool Featured,
    int? AgentId);

public record UpdateFeaturedRequest(bool Featured);

public record ChangeStatusRequest([Required] string Status);

public record ChangeListingTypeRequest([Required] string ListingType);

public record AssignAgentRequest(int? AgentId);

/// <summary>
/// A property returned by the "near you" query, with the distance from the signed-in
/// user when one could honestly be calculated.
/// </summary>
/// <param name="Property">The usual public property shape, unchanged.</param>
/// <param name="DistanceKm">
/// Great-circle distance in kilometres, or null when the user has not shared
/// coordinates and the match is by saved area instead. Null is never rendered as
/// a number, and never implied to be zero.
/// </param>
/// <param name="DistanceLabel">Pre-rounded presentation text, e.g. "2.4 km away".</param>
public record NearbyPropertyDto(
    PropertyDto Property,
    double? DistanceKm,
    string? DistanceLabel);

public record NearbyPropertiesDto(
    IReadOnlyList<NearbyPropertyDto> Items,
    /// <summary>True when the user has any saved location at all.</summary>
    bool HasLocation,
    /// <summary>
    /// True when the user shared coordinates, so the ordering is a real distance
    /// calculation. False means the match is by saved area and no km figure is shown.
    /// </summary>
    bool DistanceAvailable,
    /// <summary>The saved area's name, used for "in Jos" style wording.</summary>
    string? LocationName,
    /// <summary>How the results were derived: "distance", "area" or "none".</summary>
    string Mode);
