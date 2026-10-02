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
}
