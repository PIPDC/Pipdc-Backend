using PIPDC.Application.Common;
using PIPDC.Domain.Common;

namespace PIPDC.Application.Transactions;

public interface ITransactionService
{
    Task<Result<TransactionDto>> RecordSaleAsync(
        int propertyId, RecordSaleRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    Task<Result<TransactionDto>> RecordLeaseAsync(
        int propertyId, RecordLeaseRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    Task<Result<TransactionDto>> GetByIdAsync(
        int id, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    Task<Result<TransactionDto>> GetByPropertyAsync(
        int propertyId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    /// <summary>Admin-only list across both kinds, with filtering and paging.</summary>
    Task<Result<PaginatedResult<TransactionDto>>> GetAllAsync(
        TransactionQueryParameters query, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    /// <summary>Admin-only aggregate for the dashboard charts.</summary>
    Task<Result<TransactionAnalyticsDto>> GetAnalyticsAsync(
        DateTime? from, DateTime? to, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    /// <summary>Admin-only list of tenants currently occupying a rented property.</summary>
    Task<Result<IReadOnlyList<TenantInfoDto>>> GetTenantsAsync(
        string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    /// <summary>Admin-only list of clients who purchased a property.</summary>
    Task<Result<IReadOnlyList<PurchaserInfoDto>>> GetPurchasersAsync(
        string currentUserId, IList<string> currentUserRoles, CancellationToken ct);

    /// <summary>
    /// Admin-only. Emails a tenant the time left on their lease and stamps the
    /// reminder so the automatic worker will not immediately duplicate it.
    /// </summary>
    Task<Result<NotifyTenantResultDto>> NotifyTenantAsync(
        int leaseId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct);
}
