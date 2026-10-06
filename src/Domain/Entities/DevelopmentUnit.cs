using PIPDC.Domain.Common;
using PIPDC.Domain.Enums;

namespace PIPDC.Domain.Entities;

public class DevelopmentUnit : AuditableEntity
{
    public int DevelopmentProjectId { get; set; }
    public string UnitIdentifier { get; set; } = string.Empty;
    public string UnitType { get; set; } = string.Empty;
    public DevelopmentUnitStatus Status { get; set; }
    public decimal? Price { get; set; }
    public string Currency { get; set; } = "NGN";
    public string? Description { get; set; }

    // ── Listing descriptor ────────────────────────────────────────────────
    // A unit is an individual house: "4-Bed Semi-Detached, Plot 12". That is the
    // thing clients actually buy, so the unit carries the details a listing needs
    // and is what gets promoted to a Property. The project itself is the estate,
    // not a house, and must never be listed as one.
    public ListingType ListingType { get; set; } = ListingType.ForSale;
    public PropertyType PropertyType { get; set; } = PropertyType.Residential;

    /// <summary>Rental period, e.g. "Per annum". Null when the unit is for sale.</summary>
    public string? Period { get; set; }

    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public double? Size { get; set; }
    public string SizeUnit { get; set; } = "sqm";
    public int? YearBuilt { get; set; }
    public List<string> Amenities { get; set; } = [];

    /// <summary>
    /// The listing this unit became, once its estate is finished. One-to-one so a
    /// unit can never produce two competing listings for the same house.
    /// </summary>
    public int? PropertyId { get; set; }
    public Property? Property { get; set; }

    public DevelopmentProject Project { get; set; } = null!;
    public ICollection<DevelopmentTracking> TrackedBy { get; set; } = [];
}
