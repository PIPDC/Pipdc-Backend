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
    /// State of the lease agreement itself. Independent of
    /// <see cref="PropertyStatus.Rented"/>, which describes the listing.
    /// </summary>
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public int PropertyId { get; set; }

    public Property Property { get; set; } = null!;
}
