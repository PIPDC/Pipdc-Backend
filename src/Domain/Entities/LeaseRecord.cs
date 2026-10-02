using PIPDC.Domain.Common;
using PIPDC.Domain.Enums;

namespace PIPDC.Domain.Entities;

public class LeaseRecord : AuditableEntity
{
    public string TenantName { get; set; } = string.Empty;
    public string TenantContact { get; set; } = string.Empty;
    public decimal MonthlyRent { get; set; }
    public DateTime LeaseStartDate { get; set; }
    public DateTime LeaseEndDate { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// State of the lease agreement. Independent of
    /// <see cref="PropertyStatus.Rented"/>, which describes the listing.
    /// </summary>
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public int PropertyId { get; set; }

    /// <summary>
    /// The enquiry that led to this tenancy, when it is known. Nullable for the
    /// same reason as <see cref="SaleRecord.EnquiryId"/>: a landlord can let a
    /// property to someone they already knew.
    /// </summary>
    public int? EnquiryId { get; set; }

    /// <summary>
    /// The tenant's user account, when the tenancy came from a registered client.
    /// <see cref="TenantContact"/> remains the authoritative free-text record.
    /// </summary>
    public string? TenantUserId { get; set; }

    /// <summary>
    /// The PIPDC user who recorded the tenancy, from the JWT subject claim and
    /// never from the request body.
    /// </summary>
    public string RecordedByUserId { get; set; } = string.Empty;

    public Property Property { get; set; } = null!;
    public Enquiry? Enquiry { get; set; }
    public AppUser? Tenant { get; set; }
    public AppUser RecordedByUser { get; set; } = null!;
}
