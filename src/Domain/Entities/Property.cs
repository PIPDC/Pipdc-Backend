using PIPDC.Domain.Common;
using PIPDC.Domain.Enums;

namespace PIPDC.Domain.Entities;

public class Property : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // URL-friendly identifier for property detail routes. E.g. "luxury-3-bedroom-house-jos".
    public string Slug { get; set; } = string.Empty;

    public decimal Price { get; set; }
    public string Currency { get; set; } = "NGN";

    // Rental period (monthly, yearly, etc.). Null for properties being sold.
    public string? Period { get; set; }

    public PropertyStatus Status { get; set; }
    public PropertyType PropertyType { get; set; }
    public ListingType ListingType { get; set; }

    // =========================
    // Characteristics
    // =========================

    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public double? Size { get; set; }
    public string SizeUnit { get; set; } = "sqm";
    public double? LotSize { get; set; }
    public int? YearBuilt { get; set; }
    public List<string> Amenities { get; set; } = [];

    // =========================
    // Location
    // =========================

    public int? LocationId { get; set; }
    public Location? Location { get; set; }
    public string Address { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Area { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // =========================
    // Visibility
    // =========================

    public bool Featured { get; set; }

    // =========================
    // Ownership
    // =========================

    public int? AgentId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;

    // =========================
    // Navigation
    // =========================

    public Agent? Agent { get; set; }
    public AppUser CreatedByUser { get; set; } = null!;
    public ICollection<PropertyImage> PropertyImages { get; set; } = [];
    public ICollection<Enquiry> Enquiries { get; set; } = [];
    public ICollection<SavedProperty> SavedByUsers { get; set; } = [];
    /// <summary>
    /// A property is sold at most once in its lifetime, so the sale side stays
    /// one-to-one. Batch 7 relies on this to make double-selling impossible.
    /// </summary>
    public SaleRecord? SaleRecord { get; set; }

    /// <summary>
    /// A property is let many times over its life, so leases are one-to-many and
    /// the current tenancy is the newest non-terminated row. The previous
    /// one-to-one shape could only ever hold a single lease, which made lease
    /// history impossible to record at all.
    /// </summary>
    public ICollection<LeaseRecord> LeaseRecords { get; set; } = [];
}
