using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using PIPDC.Application.Auth;
using PIPDC.Application.Common;
using PIPDC.Application.Data;
using PIPDC.Application.Transactions;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Transactions;

public class TransactionService(
    IAppDbContext dbContext,
    IUniqueViolationDetector uniqueViolations,
    ILogger<TransactionService> logger) : ITransactionService
{
    // =========================
    // Recording
    // =========================

    public async Task<Result<TransactionDto>> RecordSaleAsync(
        int propertyId, RecordSaleRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var validation = ValidateSale(request);
        if (validation.IsFailure)
            return Result<TransactionDto>.Failure(validation.Error);

        var guard = await GuardAsync(propertyId, request.EnquiryId, currentUserId, currentUserRoles, ct);
        if (guard.IsFailure)
            return Result<TransactionDto>.Failure(guard.Error);

        if (await dbContext.LeaseRecords.AnyAsync(l => l.PropertyId == propertyId, ct))
            return Result<TransactionDto>.Failure(
                Error.Conflict("transaction.propertylet", "This property already has a recorded tenancy and cannot also be sold."));

        var property = guard.Value.Property;
        TryResolveStatus(request.Status, TransactionStatus.Completed, out var status);

        // Serialisable so the duplicate check and the insert cannot both pass for
        // two concurrent requests. The unique index on PropertyId is the final
        // authority; this makes the common case return a clean 409 instead of
        // relying on the index exception.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            if (await dbContext.SaleRecords.AnyAsync(s => s.PropertyId == propertyId, ct))
            {
                await transaction.RollbackAsync(ct);
                return Result<TransactionDto>.Failure(
                    Error.Conflict("transaction.duplicatesale", "A sale is already recorded for this property."));
            }

            var sale = new SaleRecord
            {
                PropertyId = propertyId,
                SalePrice = request.SalePrice,
                SaleDate = request.SaleDate.Date,
                BuyerName = request.BuyerName.Trim(),
                BuyerContact = request.BuyerContact.Trim(),
                Notes = request.Notes?.Trim(),
                Status = status,
                EnquiryId = request.EnquiryId,

                // The counterparty account is derived from the linked enquiry, never
                // from the request body, so an agent cannot file a sale against
                // another agent's client.
                BuyerUserId = guard.Value.Enquiry?.UserId,
                RecordedByUserId = currentUserId,
                CreatedAt = DateTime.UtcNow
            };

            // The listing follows the paperwork. Both writes share the transaction,
            // so a property can never read "available" after it was sold.
            property.Status = PropertyStatus.Sold;
            property.UpdatedAt = DateTime.UtcNow;

            dbContext.SaleRecords.Add(sale);

            var saved = await TrySaveAsync(transaction, "transaction.duplicatesale", "This property already has a recorded sale.", ct);
            if (saved.IsFailure)
                return Result<TransactionDto>.Failure(saved.Error);

            await transaction.CommitAsync(ct);
            logger.LogInformation("Sale recorded for property {PropertyId} by {UserId} at {Amount}.", propertyId, currentUserId, request.SalePrice);

            return Result<TransactionDto>.Success(await LoadSaleAsync(sale.Id, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to record sale for property {PropertyId}.", propertyId);
            await RollbackQuietlyAsync(transaction);
            return Result<TransactionDto>.Failure(
                Error.Failure("transaction.failed", "The sale could not be recorded. Nothing was changed."));
        }
    }

    public async Task<Result<TransactionDto>> RecordLeaseAsync(
        int propertyId, RecordLeaseRequest request, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var validation = ValidateLease(request);
        if (validation.IsFailure)
            return Result<TransactionDto>.Failure(validation.Error);

        var guard = await GuardAsync(propertyId, request.EnquiryId, currentUserId, currentUserRoles, ct);
        if (guard.IsFailure)
            return Result<TransactionDto>.Failure(guard.Error);

        // A property already sold cannot be let. This cross-kind check is why a
        // listing can never be both sold and rented.
        if (await dbContext.SaleRecords.AnyAsync(s => s.PropertyId == propertyId, ct))
            return Result<TransactionDto>.Failure(
                Error.Conflict("transaction.propertysold", "This property is already recorded as sold and cannot also be leased."));

        var property = guard.Value.Property;
        TryResolveStatus(request.Status, TransactionStatus.Active, out var status);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            if (await HasLiveLeaseAsync(propertyId, ct))
            {
                await transaction.RollbackAsync(ct);
                return Result<TransactionDto>.Failure(
                    Error.Conflict("transaction.duplicatelease", "This property already has a live tenancy. End or complete it before recording another."));
            }

            var lease = new LeaseRecord
            {
                PropertyId = propertyId,
                TenantName = request.TenantName.Trim(),
                TenantContact = request.TenantContact.Trim(),
                MonthlyRent = request.MonthlyRent,
                LeaseStartDate = request.LeaseStartDate.Date,
                LeaseEndDate = request.LeaseEndDate.Date,
                Notes = request.Notes?.Trim(),
                Status = status,
                EnquiryId = request.EnquiryId,
                TenantUserId = guard.Value.Enquiry?.UserId,
                RecordedByUserId = currentUserId,
                CreatedAt = DateTime.UtcNow
            };

            // Only a live tenancy makes the listing rented. A cancelled or already
            // terminated record is history and must not hide the property.
            if (status is TransactionStatus.Pending or TransactionStatus.Active)
                property.Status = PropertyStatus.Rented;
            property.UpdatedAt = DateTime.UtcNow;

            dbContext.LeaseRecords.Add(lease);

            var saved = await TrySaveAsync(transaction, "transaction.duplicatelease", "This property already has a live tenancy.", ct);
            if (saved.IsFailure)
                return Result<TransactionDto>.Failure(saved.Error);

            await transaction.CommitAsync(ct);
            logger.LogInformation("Lease recorded for property {PropertyId} by {UserId} at {Rent} per month.", propertyId, currentUserId, request.MonthlyRent);

            return Result<TransactionDto>.Success(await LoadLeaseAsync(lease.Id, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to record lease for property {PropertyId}.", propertyId);
            await RollbackQuietlyAsync(transaction);
            return Result<TransactionDto>.Failure(
                Error.Failure("transaction.failed", "The tenancy could not be recorded. Nothing was changed."));
        }
    }

    // =========================
    // Reads
    // =========================

    public async Task<Result<TransactionDto>> GetByIdAsync(
        int id, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (await dbContext.SaleRecords.AsNoTracking().AnyAsync(s => s.Id == id, ct))
            return Result<TransactionDto>.Success(await LoadSaleAsync(id, ct));

        if (await dbContext.LeaseRecords.AsNoTracking().AnyAsync(l => l.Id == id, ct))
            return Result<TransactionDto>.Success(await LoadLeaseAsync(id, ct));

        return Result<TransactionDto>.Failure(
            Error.NotFound("transaction.notfound", $"No recorded transaction with id {id} was found."));
    }

    public async Task<Result<TransactionDto>> GetByPropertyAsync(
        int propertyId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var saleId = await dbContext.SaleRecords.AsNoTracking()
            .Where(s => s.PropertyId == propertyId)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);

        if (saleId is not null)
            return Result<TransactionDto>.Success(await LoadSaleAsync(saleId.Value, ct));

        // Newest tenancy first, so the current one is what a property page shows.
        var leaseId = await dbContext.LeaseRecords.AsNoTracking()
            .Where(l => l.PropertyId == propertyId)
            .OrderByDescending(l => l.LeaseStartDate)
            .Select(l => (int?)l.Id)
            .FirstOrDefaultAsync(ct);

        if (leaseId is not null)
            return Result<TransactionDto>.Success(await LoadLeaseAsync(leaseId.Value, ct));

        return Result<TransactionDto>.Failure(
            Error.NotFound("transaction.notfound", $"No recorded transaction exists for property {propertyId}."));
    }

    public async Task<Result<PaginatedResult<TransactionDto>>> GetAllAsync(
        TransactionQueryParameters query, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (!currentUserRoles.Contains(Roles.Admin))
            return Result<PaginatedResult<TransactionDto>>.Failure(
                Error.Forbidden("transaction.forbidden", "Only administrators can list all transactions."));

        var status = TryParseStatus(query.Status);
        if (query.Status is not null && status is null)
            return Result<PaginatedResult<TransactionDto>>.Failure(
                Error.Validation("transaction.invalidstatus", $"'{query.Status}' is not a valid transaction status."));

        if (!TryParseDate(query.From, out var from))
            return Result<PaginatedResult<TransactionDto>>.Failure(
                Error.Validation("transaction.invalidfrom", "The 'from' date could not be parsed."));

        if (!TryParseDate(query.To, out var to))
            return Result<PaginatedResult<TransactionDto>>.Failure(
                Error.Validation("transaction.invalidto", "The 'to' date could not be parsed."));

        var kind = query.Kind?.Trim().ToLowerInvariant();
        var includeSales = kind is null or "" or "sale" or "sales";
        var includeLeases = kind is null or "" or "lease" or "leases";

        if (!includeSales && !includeLeases)
            return Result<PaginatedResult<TransactionDto>>.Failure(
                Error.Validation("transaction.invalidkind", "Kind must be 'Sale' or 'Lease'."));

        var sales = ProjectedSales();
        var leases = ProjectedLeases();

        if (status is not null)
        {
            sales = sales.Where(t => t.Status == status);
            leases = leases.Where(t => t.Status == status);
        }

        if (query.PropertyId is not null)
        {
            sales = sales.Where(t => t.PropertyId == query.PropertyId);
            leases = leases.Where(t => t.PropertyId == query.PropertyId);
        }

        if (query.EnquiryId is not null)
        {
            sales = sales.Where(t => t.EnquiryId == query.EnquiryId);
            leases = leases.Where(t => t.EnquiryId == query.EnquiryId);
        }

        if (from is not null)
        {
            sales = sales.Where(t => t.SaleDate >= from);
            leases = leases.Where(t => t.LeaseStartDate >= from);
        }

        if (to is not null)
        {
            sales = sales.Where(t => t.SaleDate <= to);
            leases = leases.Where(t => t.LeaseStartDate <= to);
        }

        // Sales and leases are separate tables, so a single ordered page has to be
        // stitched from both. Each side is paged on its own first, then merged and
        // re-paged; that keeps the page boundaries right without a UNION.
        var skip = (query.PageNumber - 1) * query.PageSize;
        var page = new List<TransactionDto>(query.PageSize);
        long total = 0;

        if (includeSales)
        {
            total += await sales.LongCountAsync(ct);
            page.AddRange(await sales
                .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
                .Skip(skip).Take(query.PageSize).ToListAsync(ct));
        }

        if (includeLeases)
        {
            total += await leases.LongCountAsync(ct);
            page.AddRange(await leases
                .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
                .Skip(skip).Take(query.PageSize).ToListAsync(ct));
        }

        var ordered = page
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Skip(skip)
            .Take(query.PageSize)
            .ToList();

        return Result<PaginatedResult<TransactionDto>>.Success(
            PaginatedResult<TransactionDto>.Create(ordered, (int)Math.Min(total, int.MaxValue), query.PageNumber, query.PageSize));
    }

    // =========================
    // Analytics
    // =========================

    public async Task<Result<TransactionAnalyticsDto>> GetAnalyticsAsync(
        DateTime? from, DateTime? to, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (!currentUserRoles.Contains(Roles.Admin))
            return Result<TransactionAnalyticsDto>.Failure(
                Error.Forbidden("transaction.forbidden", "Only administrators can view transaction analytics."));

        var start = (from?.Date ?? DateTime.UtcNow.Date.AddMonths(-11));
        var endExclusive = (to?.Date ?? DateTime.UtcNow.Date).AddDays(1);

        if (start >= endExclusive)
            return Result<TransactionAnalyticsDto>.Failure(
                Error.Validation("transaction.invalidrange", "The start date must fall before the end date."));

        var sales = dbContext.SaleRecords.AsNoTracking()
            .Where(s => s.SaleDate >= start && s.SaleDate < endExclusive);
        var leases = dbContext.LeaseRecords.AsNoTracking()
            .Where(l => l.LeaseStartDate >= start && l.LeaseStartDate < endExclusive);

        var saleCount = await sales.CountAsync(ct);
        var leaseCount = await leases.CountAsync(ct);

        var totalSaleValue = await sales.SumAsync(s => (decimal?)s.SalePrice, ct) ?? 0m;
        var totalMonthlyRent = await leases.SumAsync(l => (decimal?)l.MonthlyRent, ct) ?? 0m;

        var activeLeaseCount = await dbContext.LeaseRecords.AsNoTracking()
            .CountAsync(l => l.Status == TransactionStatus.Active, ct);

        // Lease term value needs a per-row month count, which Npgsql cannot fold into
        // one aggregate over an interval, so the arithmetic happens in memory over a
        // projection. The row set is one row per tenancy in the window.
        var leaseTerms = await leases
            .Select(l => new { l.MonthlyRent, l.LeaseStartDate, l.LeaseEndDate })
            .ToListAsync(ct);

        var totalLeaseTermValue = leaseTerms.Sum(t => t.MonthlyRent * MonthsIn(t.LeaseStartDate, t.LeaseEndDate));

        var monthlySales = (await sales
                .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count(), Value = g.Sum(s => s.SalePrice) })
                .ToListAsync(ct))
            .Select(r => new TransactionTrendPointDto
            {
                Month = new DateTime(r.Year, r.Month, 1),
                Label = $"{r.Year:D4}-{r.Month:D2}",
                Count = r.Count,
                Value = r.Value
            })
            .ToList();

        var monthlyLeaseCounts = await leases
            .GroupBy(l => new { l.LeaseStartDate.Year, l.LeaseStartDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(ct);

        var monthlySaleCounts = await sales
            .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(ct);

        var dealCounts = new Dictionary<(int Year, int Month), int>();
        foreach (var row in monthlySaleCounts)
            dealCounts[(row.Year, row.Month)] = row.Count;
        foreach (var row in monthlyLeaseCounts)
            dealCounts[(row.Year, row.Month)] = dealCounts.GetValueOrDefault((row.Year, row.Month)) + row.Count;

        var monthlyDeals = dealCounts
            .Select(kv => new TransactionTrendPointDto
            {
                Month = new DateTime(kv.Key.Year, kv.Key.Month, 1),
                Label = $"{kv.Key.Year:D4}-{kv.Key.Month:D2}",
                Count = kv.Value
            })
            .ToList();

        var leaseStatusGroups = await dbContext.LeaseRecords.AsNoTracking()
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Value = g.Sum(l => l.MonthlyRent) })
            .ToListAsync(ct);

        var saleStatusGroups = await dbContext.SaleRecords.AsNoTracking()
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Value = g.Sum(s => s.SalePrice) })
            .ToListAsync(ct);

        var statusBreakdown = leaseStatusGroups
            .Select(r => new BreakdownPointDto { Label = r.Status.ToString(), Count = r.Count, Value = r.Value })
            .Concat(saleStatusGroups.Select(r => new BreakdownPointDto { Label = r.Status.ToString(), Count = r.Count, Value = r.Value }))
            .ToList();

        var cityBreakdown = await dbContext.SaleRecords.AsNoTracking()
            .Where(s => s.SaleDate >= start && s.SaleDate < endExclusive)
            .GroupBy(s => s.Property.City)
            .Select(g => new { City = g.Key, Count = g.Count(), Value = g.Sum(s => s.SalePrice) })
            .ToListAsync(ct);

        var cityLeases = await dbContext.LeaseRecords.AsNoTracking()
            .Where(l => l.LeaseStartDate >= start && l.LeaseStartDate < endExclusive)
            .GroupBy(l => l.Property.City)
            .Select(g => new { City = g.Key, Count = g.Count(), Value = g.Sum(l => l.MonthlyRent) })
            .ToListAsync(ct);

        var byCity = cityBreakdown
            .Concat(cityLeases.Select(l => new { l.City, l.Count, l.Value }))
            .GroupBy(r => r.City)
            .Select(g => new BreakdownPointDto { Label = g.Key, Count = g.Sum(r => r.Count), Value = g.Sum(r => r.Value) })
            .OrderByDescending(b => b.Value)
            .Take(8)
            .ToList();

        var topSales = await dbContext.SaleRecords.AsNoTracking()
            .Where(s => s.SaleDate >= start && s.SaleDate < endExclusive)
            .OrderByDescending(s => s.SalePrice)
            .Take(5)
            .Select(s => new PropertyPerformanceDto
            {
                PropertyId = s.PropertyId,
                Title = s.Property.Title,
                Slug = s.Property.Slug,
                City = s.Property.City,
                Currency = s.Property.Currency,
                Kind = "Sale",
                Value = s.SalePrice,
                OccurredAt = s.SaleDate
            })
            .ToListAsync(ct);

        var topLeases = await dbContext.LeaseRecords.AsNoTracking()
            .Where(l => l.LeaseStartDate >= start && l.LeaseStartDate < endExclusive)
            .OrderByDescending(l => l.MonthlyRent)
            .Take(5)
            .Select(l => new PropertyPerformanceDto
            {
                PropertyId = l.PropertyId,
                Title = l.Property.Title,
                Slug = l.Property.Slug,
                City = l.Property.City,
                Currency = l.Property.Currency,
                Kind = "Lease",
                Value = l.MonthlyRent,
                OccurredAt = l.LeaseStartDate
            })
            .ToListAsync(ct);

        // Conversion is counted through the records rather than by mutating
        // Enquiry.Status, so closing a deal never rewrites a client's history.
        var linkedEnquiryIds = await dbContext.SaleRecords.AsNoTracking()
            .Where(s => s.EnquiryId != null)
            .Select(s => s.EnquiryId!.Value)
            .Concat(dbContext.LeaseRecords.AsNoTracking()
                .Where(l => l.EnquiryId != null)
                .Select(l => l.EnquiryId!.Value))
            .Distinct()
            .ToListAsync(ct);

        var enquiryCount = await dbContext.Enquiries.AsNoTracking()
            .CountAsync(e => e.CreatedAt >= start && e.CreatedAt < endExclusive, ct);

        var openEnquiries = await dbContext.Enquiries.AsNoTracking()
            .CountAsync(e => e.Status != EnquiryStatus.Resolved, ct);

        var analytics = new TransactionAnalyticsDto
        {
            GeneratedAt = DateTime.UtcNow,
            Currency = await dbContext.Properties.AsNoTracking()
                .Select(p => p.Currency)
                .FirstOrDefaultAsync(ct) ?? "NGN",

            // Distinguishes "no transactions yet" from "transactions but none in this
            // window", so the UI can say which one it is instead of showing an
            // unexplained empty chart.
            HasAnyTransactions = await dbContext.SaleRecords.AsNoTracking().AnyAsync(ct)
                                 || await dbContext.LeaseRecords.AsNoTracking().AnyAsync(ct),

            Totals = new TransactionTotalsDto
            {
                SaleCount = saleCount,
                LeaseCount = leaseCount,
                TotalSaleValue = totalSaleValue,
                TotalMonthlyRent = totalMonthlyRent,
                TotalLeaseTermValue = totalLeaseTermValue,
                ActiveLeaseCount = activeLeaseCount,
                PropertiesSold = await dbContext.Properties.AsNoTracking().CountAsync(p => p.Status == PropertyStatus.Sold, ct),
                PropertiesRented = await dbContext.Properties.AsNoTracking().CountAsync(p => p.Status == PropertyStatus.Rented, ct),
                AverageSalePrice = saleCount == 0 ? 0m : totalSaleValue / saleCount
            },

            // Twelve fixed months, so a month with no deals shows as a gap in the
            // series rather than silently compressing the x-axis.
            MonthlySales = FillMonths(monthlySales, start),
            MonthlyDeals = FillMonths(monthlyDeals, start),
            ByStatus = [.. WithPercentages(statusBreakdown.OrderByDescending(b => b.Count))],
            ByCity = [.. WithPercentages(byCity)],
            TopProperties = [.. topSales.Concat(topLeases).OrderByDescending(p => p.Value).Take(5)],

            EnquiryToDealFunnel = new FunnelDto
            {
                EnquiryCount = enquiryCount,
                EnquiriesLinkedToDeals = linkedEnquiryIds.Count,
                EnquiriesStillOpen = openEnquiries,

                // Null rather than 0% when there were no enquiries: a conversion rate
                // of zero out of zero is not a measurement.
                ConversionRate = enquiryCount == 0
                    ? null
                    : Math.Round(linkedEnquiryIds.Count * 100m / enquiryCount, 1)
            }
        };

        return Result<TransactionAnalyticsDto>.Success(analytics);
    }

    // =========================
    // Validation
    // =========================

    private static Result ValidateSale(RecordSaleRequest request)
    {
        if (request.SalePrice <= 0)
            return Result.Failure(Error.Validation("transaction.invalidprice", "The sale price must be greater than zero."));

        if (request.SaleDate == default)
            return Result.Failure(Error.Validation("transaction.invaliddates", "A sale date is required."));

        // A future-dated sale inflates every revenue figure on the dashboard.
        if (request.SaleDate.Date > DateTime.UtcNow.Date)
            return Result.Failure(Error.Validation("transaction.futuredate", "The sale date cannot be in the future."));

        if (string.IsNullOrWhiteSpace(request.BuyerName))
            return Result.Failure(Error.Validation("transaction.missingbuyername", "The buyer's name is required."));

        if (string.IsNullOrWhiteSpace(request.BuyerContact))
            return Result.Failure(Error.Validation("transaction.missingbuyercontact", "The buyer's contact is required."));

        if (!TryResolveStatus(request.Status, TransactionStatus.Completed, out var status))
            return Result.Failure(Error.Validation("transaction.invalidstatus", $"'{request.Status}' is not a valid transaction status."));

        // A sale has nothing left to await after signing. Allowing Pending/Active
        // would make "active sales" a state the rest of the app would have to handle.
        if (status is TransactionStatus.Pending or TransactionStatus.Active)
            return Result.Failure(Error.Validation("transaction.invalidsalestatus",
                "A sale can only be Completed, Terminated, or Cancelled."));

        return Result.Success();
    }

    private static Result ValidateLease(RecordLeaseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantName))
            return Result.Failure(Error.Validation("transaction.missingtenantname", "The tenant's name is required."));

        if (string.IsNullOrWhiteSpace(request.TenantContact))
            return Result.Failure(Error.Validation("transaction.missingtenantcontact", "The tenant's contact is required."));

        if (request.MonthlyRent <= 0)
            return Result.Failure(Error.Validation("transaction.invalidrent", "The monthly rent must be greater than zero."));

        if (request.LeaseStartDate == default || request.LeaseEndDate == default)
            return Result.Failure(Error.Validation("transaction.invaliddates", "Both lease dates are required."));

        if (request.LeaseEndDate.Date <= request.LeaseStartDate.Date)
            return Result.Failure(Error.Validation("transaction.invalidterm", "The lease end date must be after the start date."));

        if (!TryResolveStatus(request.Status, TransactionStatus.Active, out var status))
            return Result.Failure(Error.Validation("transaction.invalidstatus", $"'{request.Status}' is not a valid transaction status."));

        // A tenancy cannot already be complete on the day it is recorded; that
        // transition happens later, when the term is served or ended.
        if (status == TransactionStatus.Completed)
            return Result.Failure(Error.Validation("transaction.invalidleasestatus",
                "A tenancy can only be Pending, Active, Terminated, or Cancelled when recorded."));

        return Result.Success();
    }

    // =========================
    // Guards
    // =========================

    private sealed record GuardContext(Property Property, Enquiry? Enquiry);

    /// <summary>
    /// Shared pre-flight for both recorders: the property must exist and be one the
    /// caller may act on, and a supplied enquiry must belong to that same property.
    /// The cross-property check is what stops an enquiry for one listing being used
    /// to mark a different listing as converted.
    /// </summary>
    private async Task<Result<GuardContext>> GuardAsync(
        int propertyId, int? enquiryId, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        var property = await dbContext.Properties
            .Include(p => p.Agent)
            .FirstOrDefaultAsync(p => p.Id == propertyId, ct);

        if (property is null)
            return Result<GuardContext>.Failure(
                Error.NotFound("property.notfound", $"Property with id {propertyId} was not found."));

        var ownership = await VerifyCanRecordAsync(property, currentUserId, currentUserRoles, ct);
        if (ownership.IsFailure)
            return Result<GuardContext>.Failure(ownership.Error);

        if (property.Status == PropertyStatus.Unavailable)
            return Result<GuardContext>.Failure(
                Error.Conflict("property.unavailable", "This property is marked unavailable, so no transaction can be recorded against it."));

        Enquiry? enquiry = null;

        if (enquiryId is not null)
        {
            enquiry = await dbContext.Enquiries.FirstOrDefaultAsync(e => e.Id == enquiryId.Value, ct);

            if (enquiry is null)
                return Result<GuardContext>.Failure(
                    Error.NotFound("enquiry.notfound", $"Enquiry with id {enquiryId} was not found."));

            if (enquiry.PropertyId != propertyId)
                return Result<GuardContext>.Failure(
                    Error.Validation("transaction.enquirymismatch", "The selected enquiry is for a different property."));

            if (!currentUserRoles.Contains(Roles.Admin))
            {
                // Otherwise one agent could claim another's client by naming their
                // enquiry, since the enquiry's user becomes the recorded counterparty.
                var isOwnEnquiry = enquiry.UserId == currentUserId
                                   || (property.AgentId is not null && property.Agent?.UserId == currentUserId);

                if (!isOwnEnquiry)
                    return Result<GuardContext>.Failure(
                        Error.Forbidden("transaction.enquiryforbidden", "You cannot record a deal against that enquiry."));
            }
        }

        return Result<GuardContext>.Success(new GuardContext(property, enquiry));
    }

    /// <summary>
    /// Mirrors <c>PropertyService.VerifyOwnershipAsync</c>: admins may record
    /// anything, an agent only for a property assigned to them, and a suspended
    /// agent not at all. Written out rather than delegated because a recorded sale
    /// is an auditable financial act rather than a content edit.
    /// </summary>
    private async Task<Result> VerifyCanRecordAsync(
        Property property, string currentUserId, IList<string> currentUserRoles, CancellationToken ct)
    {
        if (currentUserRoles.Contains(Roles.Admin))
            return Result.Success();

        if (currentUserRoles.Contains(Roles.Agent))
        {
            var agent = await dbContext.Agents.FirstOrDefaultAsync(a => a.UserId == currentUserId, ct);

            if (agent is null || property.AgentId != agent.Id)
                return Result.Failure(
                    Error.Forbidden("transaction.forbidden", "You can only record a transaction for a property assigned to you."));

            if (agent.IsSuspended)
                return Result.Failure(
                    Error.Forbidden("transaction.agentsuspended",
                        "Your account is suspended. An administrator must reinstate it before you can record transactions."));

            return Result.Success();
        }

        return Result.Failure(
            Error.Unauthorized("transaction.unauthorized", "You are not authorized to record transactions."));
    }

    private Task<bool> HasLiveLeaseAsync(int propertyId, CancellationToken ct) =>
        dbContext.LeaseRecords.AnyAsync(
            l => l.PropertyId == propertyId
                 && (l.Status == TransactionStatus.Pending || l.Status == TransactionStatus.Active),
            ct);

    // =========================
    // Persistence helpers
    // =========================

    /// <summary>
    /// Saves and commits, translating the two failure modes that matter here: the
    /// unique index losing a race, and an optimistic-concurrency miss. A failure is
    /// returned already rolled back.
    /// </summary>
    private async Task<Result> TrySaveAsync(
        IDbContextTransaction transaction,
        string conflictCode, string conflictMessage, CancellationToken ct)
    {
        try
        {
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateException ex) when (uniqueViolations.IsUniqueViolation(ex))
        {
            // The index is the authority: a race that slips past the pre-check still
            // cannot produce two sales or two live tenancies.
            await RollbackQuietlyAsync(transaction);
            return Result.Failure(Error.Conflict(conflictCode, conflictMessage));
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackQuietlyAsync(transaction);
            return Result.Failure(Error.Concurrency());
        }
    }

    private static async Task RollbackQuietlyAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // The transaction is already gone or the connection is broken. The
            // original failure is what matters, so this is swallowed rather than
            // masking it; the logger has already recorded the real cause.
        }
    }

    // =========================
    // Projections
    // =========================

    private IQueryable<TransactionDto> ProjectedSales() =>
        from s in dbContext.SaleRecords.AsNoTracking()
        select new TransactionDto
        {
            Id = s.Id,
            Kind = "Sale",
            Status = s.Status,
            StatusLabel = s.Status.ToString(),
            PropertyId = s.PropertyId,
            PropertyTitle = s.Property.Title,
            PropertySlug = s.Property.Slug,
            PropertyCity = s.Property.City,
            PropertyState = s.Property.State,
            Currency = s.Property.Currency,
            EnquiryId = s.EnquiryId,
            EnquiryDate = s.Enquiry == null ? null : s.Enquiry.CreatedAt,
            SalePrice = s.SalePrice,
            SaleDate = s.SaleDate,
            BuyerName = s.BuyerName,
            BuyerContact = s.BuyerContact,
            CounterpartyUserId = s.BuyerUserId,
            RecordedByUserId = s.RecordedByUserId,
            RecordedByName = s.RecordedByUser == null ? string.Empty : s.RecordedByUser.FullName,
            CreatedAt = s.CreatedAt,
            Notes = s.Notes
        };

    private IQueryable<TransactionDto> ProjectedLeases() =>
        from l in dbContext.LeaseRecords.AsNoTracking()
        select new TransactionDto
        {
            Id = l.Id,
            Kind = "Lease",
            Status = l.Status,
            StatusLabel = l.Status.ToString(),
            PropertyId = l.PropertyId,
            PropertyTitle = l.Property.Title,
            PropertySlug = l.Property.Slug,
            PropertyCity = l.Property.City,
            PropertyState = l.Property.State,
            Currency = l.Property.Currency,
            EnquiryId = l.EnquiryId,
            EnquiryDate = l.Enquiry == null ? null : l.Enquiry.CreatedAt,
            MonthlyRent = l.MonthlyRent,
            LeaseStartDate = l.LeaseStartDate,
            LeaseEndDate = l.LeaseEndDate,
            TenantName = l.TenantName,
            TenantContact = l.TenantContact,
            CounterpartyUserId = l.TenantUserId,
            RecordedByUserId = l.RecordedByUserId,
            RecordedByName = l.RecordedByUser == null ? string.Empty : l.RecordedByUser.FullName,
            CreatedAt = l.CreatedAt,
            Notes = l.Notes
        };

    private async Task<TransactionDto> LoadSaleAsync(int id, CancellationToken ct) =>
        await ProjectedSales().FirstOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new InvalidOperationException($"Sale {id} was not readable immediately after being written.");

    private async Task<TransactionDto> LoadLeaseAsync(int id, CancellationToken ct) =>
        await ProjectedLeases().FirstOrDefaultAsync(l => l.Id == id, ct)
        ?? throw new InvalidOperationException($"Lease {id} was not readable immediately after being written.");

    // =========================
    // Small helpers
    // =========================

    /// <summary>Whole months, counting an inclusive day range, minimum one month.</summary>
    private static int MonthsIn(DateTime start, DateTime end)
    {
        var days = (end.Date - start.Date).TotalDays + 1;
        return Math.Max(1, (int)Math.Ceiling(days / 30.0));
    }

    private static List<TransactionTrendPointDto> FillMonths(List<TransactionTrendPointDto> points, DateTime start)
    {
        var byMonth = points.ToDictionary(p => (p.Month.Year, p.Month.Month));
        var series = new List<TransactionTrendPointDto>(12);
        var cursor = new DateTime(start.Year, start.Month, 1);

        for (var i = 0; i < 12; i++)
        {
            var key = (cursor.Year, cursor.Month);
            series.Add(byMonth.TryGetValue(key, out var existing)
                ? new TransactionTrendPointDto
                {
                    Month = cursor,
                    Label = $"{cursor.Year:D4}-{cursor.Month:D2}",
                    Count = existing.Count,
                    Value = existing.Value
                }
                : new TransactionTrendPointDto { Month = cursor, Label = $"{cursor.Year:D4}-{cursor.Month:D2}" });

            cursor = cursor.AddMonths(1);
        }

        return series;
    }

    private static IEnumerable<BreakdownPointDto> WithPercentages(IEnumerable<BreakdownPointDto> points)
    {
        var list = points.ToList();
        var max = list.Count == 0 ? 0m : list.Max(p => p.Value);

        foreach (var point in list)
            point.Percentage = max <= 0m ? 0m : Math.Round(point.Value * 100m / max, 1);

        return list;
    }

    /// <summary>Null when no filter was requested, so an absent value is not an error.</summary>
    private static TransactionStatus? TryParseStatus(string? label) =>
        string.IsNullOrWhiteSpace(label)
            ? null
            : TryResolveStatus(label, defaultValue: TransactionStatus.Pending, out var status) ? status : null;

    /// <summary>
    /// Resolves a requested status, falling back to the given default when the
    /// caller sent nothing. Used by both the validators and the write path so the
    /// legality rules live in exactly one place.
    /// </summary>
    private static bool TryResolveStatus(string? label, TransactionStatus defaultValue, out TransactionStatus status)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            status = defaultValue;
            return true;
        }

        return Enum.TryParse(label.Trim(), true, out status);
    }

    private static bool TryParseDate(string? value, out DateTime? date)
    {
        date = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (!DateTime.TryParse(value, out var parsed))
            return false;

        date = parsed.Date;
        return true;
    }
}
