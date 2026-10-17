using PIPDC.Domain.Enums;

namespace PIPDC.Application.Transactions;

/// <summary>
/// A tenant currently occupying one of our rented properties, with the lease
/// term and the contact details needed to reach them. <see cref="DaysRemaining"/>
/// is computed server-side so every client shows the same countdown.
/// </summary>
public class TenantInfoDto
{
    public int LeaseId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string TenantEmail { get; set; } = string.Empty;
    public string TenantPhone { get; set; } = string.Empty;
    public string? TenantUserId { get; set; }

    public int PropertyId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string PropertySlug { get; set; } = string.Empty;
    public string PropertyCity { get; set; } = string.Empty;
    public string PropertyState { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;

    public decimal MonthlyRent { get; set; }
    public DateTime LeaseStartDate { get; set; }
    public DateTime LeaseEndDate { get; set; }
    public TransactionStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;

    /// <summary>Whole days until the lease ends; negative once it has passed.</summary>
    public int DaysRemaining { get; set; }

    /// <summary>True when the rent-expiry reminder is already due (within a month).</summary>
    public bool ReminderDue { get; set; }

    /// <summary>When a reminder was last sent, if one ever was.</summary>
    public DateTime? ReminderSentAt { get; set; }
}

/// <summary>A client who purchased one of our properties, with their contact details.</summary>
public class PurchaserInfoDto
{
    public int SaleId { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;
    public string BuyerPhone { get; set; } = string.Empty;
    public string? BuyerUserId { get; set; }

    public int PropertyId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string PropertySlug { get; set; } = string.Empty;
    public string PropertyCity { get; set; } = string.Empty;
    public string PropertyState { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;

    public decimal SalePrice { get; set; }
    public DateTime SaleDate { get; set; }
    public TransactionStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
}

/// <summary>Outcome of a manual tenant notification, for the admin UI toast.</summary>
public class NotifyTenantResultDto
{
    public int LeaseId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string TenantEmail { get; set; } = string.Empty;
    public int DaysRemaining { get; set; }
}
