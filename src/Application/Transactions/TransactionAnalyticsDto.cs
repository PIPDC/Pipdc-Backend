namespace PIPDC.Application.Transactions;

/// <summary>
/// Batch 7 analytics. Every figure here is a SQL aggregate over
/// <c>SaleRecords</c>/<c>LeaseRecords</c>/<c>Enquiries</c>, so the charts cannot
/// drift from the data. Where there is no data the series is simply empty, which
/// the frontend renders as an explicit "no data" state rather than a flat zero
/// line that reads as "we recorded nothing and everything is fine".
/// </summary>
public class TransactionAnalyticsDto
{
    public DateTime GeneratedAt { get; set; }
    public string Currency { get; set; } = "NGN";

    public TransactionTotalsDto Totals { get; set; } = new();

    /// <summary>Monthly sale value and count, oldest first, for the trend chart.</summary>
    public List<TransactionTrendPointDto> MonthlySales { get; set; } = [];

    /// <summary>Monthly deals closed (sale or lease), oldest first.</summary>
    public List<TransactionTrendPointDto> MonthlyDeals { get; set; } = [];

    public List<BreakdownPointDto> ByStatus { get; set; } = [];

    public List<BreakdownPointDto> ByCity { get; set; } = [];

    public List<PropertyPerformanceDto> TopProperties { get; set; } = [];

    public FunnelDto EnquiryToDealFunnel { get; set; } = new();

    /// <summary>
    /// True when the underlying tables have no transactions at all. The frontend
    /// uses this to explain an empty dashboard instead of implying bad news.
    /// </summary>
    public bool HasAnyTransactions { get; set; }
}

public class TransactionTotalsDto
{
    public int SaleCount { get; set; }
    public int LeaseCount { get; set; }

    public decimal TotalSaleValue { get; set; }
    public decimal TotalMonthlyRent { get; set; }

    /// <summary>Signed lease value over the recorded term, for a like-for-like total.</summary>
    public decimal TotalLeaseTermValue { get; set; }

    public int ActiveLeaseCount { get; set; }
    public int PropertiesSold { get; set; }
    public int PropertiesRented { get; set; }

    /// <summary>Average sale price across recorded sales, not a projection.</summary>
    public decimal AverageSalePrice { get; set; }
}

public class TransactionTrendPointDto
{
    /// <summary>First day of the month, e.g. 2026-01-01.</summary>
    public DateTime Month { get; set; }

    /// <summary>"2026-01", pre-formatted so the chart does not re-derive labels.</summary>
    public string Label { get; set; } = string.Empty;

    public int Count { get; set; }
    public decimal Value { get; set; }
}

public class BreakdownPointDto
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Value { get; set; }

    /// <summary>0-100, computed server-side against the largest value in the set.</summary>
    public decimal Percentage { get; set; }
}

public class PropertyPerformanceDto
{
    public int PropertyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime? OccurredAt { get; set; }
}

public class FunnelDto
{
    public int EnquiryCount { get; set; }
    public int EnquiriesLinkedToDeals { get; set; }
    public int EnquiriesStillOpen { get; set; }

    /// <summary>
    /// Percentage of enquiries that ended in a recorded deal, or null when there
    /// were no enquiries to divide by. Null is shown as "no data" rather than 0%.
    /// </summary>
    public decimal? ConversionRate { get; set; }
}
