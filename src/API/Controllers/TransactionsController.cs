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
}
