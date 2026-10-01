namespace PIPDC.Domain.Enums;

/// <summary>
/// Lifecycle of a recorded property transaction. Shared by <c>SaleRecord</c> and
/// <c>LeaseRecord</c> so a sale and a lease speak the same vocabulary.
///
/// The property's own market state continues to live in <see cref="PropertyStatus"/>
/// (available / pending / sold / rented / unavailable). This enum describes the
/// transaction paperwork, which is a separate concern.
/// </summary>
public enum TransactionStatus
{
    /// <summary>Agreed in principle but not yet finalised.</summary>
    Pending = 0,

    /// <summary>Currently in force. The normal state for an ongoing lease.</summary>
    Active = 1,

    /// <summary>Finished normally. A completed sale, or a lease that ran its full term.</summary>
    Completed = 2,

    /// <summary>Ended early, e.g. a lease terminated before <c>LeaseEndDate</c>.</summary>
    Terminated = 3,

    /// <summary>Abandoned before completion. No money changed hands.</summary>
    Cancelled = 4
}
