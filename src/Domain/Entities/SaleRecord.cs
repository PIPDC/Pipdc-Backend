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

    /// <summary>
    /// The enquiry that led to this sale, when it is known. Nullable on purpose:
    /// a sale can be completed by direct negotiation without ever raising an
    /// enquiry, and refusing to record it because of that would be worse than
    /// recording it without a link. It is the join that makes enquiry-to-deal
    /// conversion measurable.
    /// </summary>
    public int? EnquiryId { get; set; }

    /// <summary>
    /// The user account of the buyer, when the sale came from a registered client.
    /// Distinct from <see cref="BuyerContact"/>, which is a free-text name and
    /// contact supplied at the time of signing and is always populated.
    /// </summary>
    public string? BuyerUserId { get; set; }

    /// <summary>
    /// The PIPDC user who recorded the sale, taken from the JWT subject claim
    /// rather than the request body. Never null: attribution is the point of an
    /// explicit confirmation, otherwise "who sold this" is unanswerable.
    /// </summary>
    public string RecordedByUserId { get; set; } = string.Empty;

    public Property Property { get; set; } = null!;
    public Enquiry? Enquiry { get; set; }
    public AppUser? Buyer { get; set; }
    public AppUser RecordedByUser { get; set; } = null!;
}
