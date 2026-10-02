using PIPDC.Domain.Enums;

namespace PIPDC.Application.Transactions;

public class TransactionQueryParameters
{
    private const int MaxPageSize = 100;

    private int _pageNumber = 1;
    private int _pageSize = 20;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 20,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    /// <summary>"Sale", "Lease", or empty for both.</summary>
    public string? Kind { get; set; }

    /// <summary>Parsed in the service so a bad value returns 400 rather than silently matching nothing.</summary>
    public string? Status { get; set; }

    public int? PropertyId { get; set; }

    public int? EnquiryId { get; set; }

    public string? From { get; set; }

    public string? To { get; set; }
}

public class TransactionDto
{
    public int Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public TransactionStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;

    public int PropertyId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string PropertySlug { get; set; } = string.Empty;
    public string PropertyCity { get; set; } = string.Empty;
    public string PropertyState { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;

    public int? EnquiryId { get; set; }
    public DateTime? EnquiryDate { get; set; }

    // Sale-specific.
    public decimal? SalePrice { get; set; }
    public DateTime? SaleDate { get; set; }
    public string? BuyerName { get; set; }
    public string? BuyerContact { get; set; }

    // Lease-specific.
    public decimal? MonthlyRent { get; set; }
    public DateTime? LeaseStartDate { get; set; }
    public DateTime? LeaseEndDate { get; set; }
    public string? TenantName { get; set; }
    public string? TenantContact { get; set; }

    public string? CounterpartyUserId { get; set; }
    public string RecordedByUserId { get; set; } = string.Empty;
    public string RecordedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }
}
