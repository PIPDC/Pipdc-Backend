using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;
using PIPDC.Infrastructure.Data;

namespace PIPDC.Infrastructure.Idempotency;

/// <summary>
/// EF-backed idempotency for side-effect POST endpoints.
///
/// The client sends a unique <c>Idempotency-Key</c> header per logical operation
/// and reuses it when retrying. The first attempt is recorded ("reserved") before
/// the action runs; the unique (UserId, Key) / (Key) indexes below arbitrate
/// concurrent duplicates. A repeated key returns the originally stored status code
/// and body instead of re-running the action, so retries and double-clicks cannot
/// create duplicate enquiries, messages, or emails.
///
/// A request that fails (non-2xx result or thrown exception) has its reservation
/// removed, so a genuine retry with the same key re-executes. Keys live 24h; expired
/// rows are purged opportunistically on each call.
/// </summary>
public sealed class IdempotentAttribute : ActionFilterAttribute
{
    public const string HeaderName = "Idempotency-Key";

    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var ct = context.HttpContext.RequestAborted;

        if (!TryGetKey(context, out var key))
            return;

        // Anonymous endpoints (auth POSTs) have no Sub claim; they share the global
        // anonymous scope enforced by the filtered unique index.
        var userId = context.HttpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var requestHash = ComputeRequestHash(context);

        await PurgeExpiredAsync(dbContext, ct);

        var existing = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == key, ct);

        if (existing is not null)
        {
            if (existing.Status == IdempotencyStatus.Completed)
            {
                if (existing.RequestHash != requestHash)
                {
                    context.Result = Conflict(
                        "idempotency.keyreuse",
                        $"This {HeaderName} was already used for a different request.");
                    return;
                }

                Replay(existing, context);
                return;
            }

            SetRetryAfter(context.HttpContext);
            context.Result = Conflict(
                "idempotency.inprogress",
                $"A request with this {HeaderName} is already being processed.");
            return;
        }

        var reservation = new IdempotencyRecord
        {
            UserId = userId,
            Key = key,
            RequestHash = requestHash,
            Status = IdempotencyStatus.Processing,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(Ttl)
        };

        dbContext.IdempotencyRecords.Add(reservation);
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent request claimed the key first; the unique index won.
            // Detach the phantom before responding so it cannot be re-saved later.
            dbContext.Entry(reservation).State = EntityState.Detached;

            var stored = await dbContext.IdempotencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == key, ct);

            if (stored is not null && stored.Status == IdempotencyStatus.Completed)
            {
                if (stored.RequestHash != requestHash)
                {
                    context.Result = Conflict(
                        "idempotency.keyreuse",
                        $"This {HeaderName} was already used for a different request.");
                    return;
                }

                Replay(stored, context);
                return;
            }

            SetRetryAfter(context.HttpContext);
            context.Result = Conflict(
                "idempotency.inprogress",
                $"A request with this {HeaderName} is already being processed.");
            return;
        }

        ActionExecutedContext executed;
        try
        {
            executed = await next();
        }
        catch
        {
            // Surface the failure, but forget the reservation first so a retry with
            // the same key genuinely re-runs the action.
            await RemoveReservationAsync(dbContext, reservation, ct);
            throw;
        }

        if (TryCaptureResult(executed.Result, out var statusCode, out var body))
        {
            reservation.Status = IdempotencyStatus.Completed;
            reservation.ResponseStatusCode = statusCode;
            reservation.ResponseBody = body;
            reservation.CompletedAt = DateTime.UtcNow;
        }
        else
        {
            await RemoveReservationAsync(dbContext, reservation, ct);
        }
    }

    private static bool TryGetKey(ActionExecutingContext context, out string key)
    {
        key = string.Empty;
        var header = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(header))
        {
            context.Result = new BadRequestObjectResult(Error.Validation(
                "idempotency.missingkey", $"The {HeaderName} header is required."));
            return false;
        }

        if (!Guid.TryParse(header, out var guid))
        {
            context.Result = new BadRequestObjectResult(Error.Validation(
                "idempotency.invalidkey", $"The {HeaderName} header must be a valid GUID."));
            return false;
        }

        key = guid.ToString("N");
        return true;
    }

    private static async Task PurgeExpiredAsync(AppDbContext dbContext, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await dbContext.IdempotencyRecords
            .Where(r => r.ExpiresAt < now)
            .ExecuteDeleteAsync(ct);
    }

    private static string ComputeRequestHash(ActionExecutingContext context)
    {
        var path = context.HttpContext.Request.Path.ToString();
        var args = context.ActionArguments
            .Where(a => a.Value is not CancellationToken)
            .OrderBy(a => a.Key, StringComparer.Ordinal)
            .ToDictionary(a => a.Key, a => a.Value);

        var serialized = JsonSerializer.Serialize(args, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes($"{path}|{serialized}");
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static void Replay(IdempotencyRecord stored, ActionExecutingContext context) =>
        context.Result = new ContentResult
        {
            StatusCode = stored.ResponseStatusCode,
            ContentType = "application/json",
            Content = stored.ResponseBody ?? string.Empty
        };

    private static ObjectResult Conflict(string code, string message) =>
        new(Error.Conflict(code, message)) { StatusCode = StatusCodes.Status409Conflict };

    private static void SetRetryAfter(HttpContext context) =>
        context.Response.Headers.RetryAfter = "1";

    private static bool TryCaptureResult(IActionResult? result, out int statusCode, out string body)
    {
        statusCode = StatusCodes.Status200OK;
        body = string.Empty;

        if (result is not ObjectResult obj || obj.Value is null)
            return false;

        statusCode = obj.StatusCode ?? StatusCodes.Status200OK;
        if (statusCode is < StatusCodes.Status200OK or >= StatusCodes.Status300MultipleChoices)
            return false;

        body = JsonSerializer.Serialize(obj.Value, JsonOpts);
        return true;
    }

    private static async Task RemoveReservationAsync(AppDbContext dbContext, IdempotencyRecord record, CancellationToken ct)
    {
        if (dbContext.Entry(record).State == EntityState.Detached)
            return;

        dbContext.IdempotencyRecords.Remove(record);
        await dbContext.SaveChangesAsync(ct);
    }
}