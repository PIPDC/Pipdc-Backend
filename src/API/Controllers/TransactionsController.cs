using Asp.Versioning;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.Auth;
using PIPDC.Application.Transactions;
using PIPDC.Application.Idempotency;
using PIPDC.Infrastructure.RateLimiting;

namespace PIPDC.API.Controllers;

/// <summary>
/// Batch 7. Recording a sale or a tenancy is an attributed financial act, so these
/// routes are agent/admin only, take no user identifiers in the body, and are
/// protected against double submits by the existing [Idempotent] filter.
/// </summary>
[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/transactions")]
public class TransactionsController(ITransactionService transactionService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    private IList<string> CurrentUserRoles => User.FindAll("role").Select(c => c.Value).ToList();

    /// <summary>Records a completed sale and marks the property sold, atomically.</summary>
    [HttpPost("properties/{propertyId:int}/sales")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Agent}")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    [Idempotent]
    public async Task<IActionResult> RecordSale(int propertyId, [FromBody] RecordSaleRequest request, CancellationToken ct)
    {
        var result = await transactionService.RecordSaleAsync(propertyId, request, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    /// <summary>Records a tenancy and marks the property rented, atomically.</summary>
    [HttpPost("properties/{propertyId:int}/leases")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Agent}")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    [Idempotent]
    public async Task<IActionResult> RecordLease(int propertyId, [FromBody] RecordLeaseRequest request, CancellationToken ct)
    {
        var result = await transactionService.RecordLeaseAsync(propertyId, request, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await transactionService.GetByIdAsync(id, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    [HttpGet("properties/{propertyId:int}")]
    public async Task<IActionResult> GetByProperty(int propertyId, CancellationToken ct)
    {
        var result = await transactionService.GetByPropertyAsync(propertyId, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    /// <summary>Admin-only list across sales and tenancies.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetAll([FromQuery] TransactionQueryParameters query, CancellationToken ct)
    {
        var result = await transactionService.GetAllAsync(query, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    /// <summary>Admin-only aggregates that back the dashboard charts.</summary>
    [HttpGet("analytics")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetAnalytics([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var result = await transactionService.GetAnalyticsAsync(from, to, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    /// <summary>Admin-only roster of tenants currently occupying a rented property.</summary>
    [HttpGet("tenants")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetTenants(CancellationToken ct)
    {
        var result = await transactionService.GetTenantsAsync(CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    /// <summary>Admin-only roster of clients who purchased a property.</summary>
    [HttpGet("purchasers")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetPurchasers(CancellationToken ct)
    {
        var result = await transactionService.GetPurchasersAsync(CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Admin-only. Emails the tenant the time left on their lease. [Idempotent]
    /// so a double-click replays the first response instead of mailing twice.
    /// </summary>
    [HttpPost("leases/{leaseId:int}/notify")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    [Idempotent]
    public async Task<IActionResult> NotifyTenant(int leaseId, CancellationToken ct)
    {
        var result = await transactionService.NotifyTenantAsync(leaseId, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Admin-only. Extends a live tenancy's end date and clears its rent reminder.
    /// Not [Idempotent]: renewing twice to the same date is already a safe no-op,
    /// so no replay key is needed.
    /// </summary>
    [HttpPost("leases/{leaseId:int}/renew")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> RenewLease(int leaseId, [FromBody] RenewLeaseRequest request, CancellationToken ct)
    {
        var result = await transactionService.RenewLeaseAsync(leaseId, request.NewEndDate, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Admin-only. Ends a tenancy because the tenant has left and returns the
    /// property to the market. This is the only supported way to free a rented
    /// listing, which is why the property status cannot be edited to Available
    /// while a live lease is on file.
    /// </summary>
    [HttpPost("leases/{leaseId:int}/end")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> EndLease(int leaseId, CancellationToken ct)
    {
        var result = await transactionService.EndLeaseAsync(leaseId, CurrentUserId, CurrentUserRoles, ct);
        return result.ToActionResult();
    }
}
