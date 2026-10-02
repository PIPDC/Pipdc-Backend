namespace PIPDC.Application.Transactions;

/// <summary>
/// Batch 7. A recorded sale or lease is a deliberate, attributed act, so these
/// requests deliberately carry no user identifiers: the recorder comes from the
/// JWT, and the buyer/tenant account is derived from the linked enquiry. A body
/// that could name an arbitrary user would let one agent file a sale against
/// another agent's client.
/// </summary>
public class RecordSaleRequest
{
    public decimal SalePrice { get; set; }
    public DateTime SaleDate { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerContact { get; set; } = string.Empty;
    public string? Notes { get; set; }

    /// <summary>Defaults to Completed, which is what "we sold it" means.</summary>
    public string? Status { get; set; }

    /// <summary>
    /// Optional. When supplied, it must be an enquiry on the same property, and
    /// its registered user becomes the buyer. This is the only way a
    /// counterparty account gets attached, so attribution cannot be forged.
    /// </summary>
    public int? EnquiryId { get; set; }
}

public class RecordLeaseRequest
{
    public string TenantName { get; set; } = string.Empty;
    public string TenantContact { get; set; } = string.Empty;
    public decimal MonthlyRent { get; set; }
    public DateTime LeaseStartDate { get; set; }
    public DateTime LeaseEndDate { get; set; }
    public string? Notes { get; set; }

    /// <summary>Defaults to Active, which is what "the lease is signed" means.</summary>
    public string? Status { get; set; }

    /// <summary>See <see cref="RecordSaleRequest.EnquiryId"/>.</summary>
    public int? EnquiryId { get; set; }
}
