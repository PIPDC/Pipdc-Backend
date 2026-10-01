using PIPDC.Domain.Common;
using PIPDC.Domain.Enums;

namespace PIPDC.Domain.Entities;

public class SaleRecord : AuditableEntity
{
    public decimal SalePrice { get; set; }
    public DateTime SaleDate { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerContact { get; set; } = string.Empty;
    public string? Notes { get; set; }

    /// <summary>
    /// State of the sale transaction. Independent of
    /// <see cref="PropertyStatus.Sold"/>, which describes the listing.
    /// </summary>
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public int PropertyId { get; set; }

    public Property Property { get; set; } = null!;
}
