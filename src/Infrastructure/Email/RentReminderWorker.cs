using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PIPDC.Application.Data;
using PIPDC.Application.Email;
using PIPDC.Application.Transactions;
using PIPDC.Domain.Enums;

namespace PIPDC.Infrastructure.Email;

/// <summary>
/// Sends tenants a rent-expiry reminder automatically once their lease is inside
/// the notice window for its length (a week for a short let, a month for a longer
/// one).
/// </summary>
/// <remarks>
/// <para>
/// The scan is periodic rather than scheduled per lease: a tenancy can be edited
/// or its dates changed at any time, so a durable per-lease timer would need to
/// be kept in step with those edits. Instead the worker looks for leases already
/// inside the notice window and not yet reminded.
/// </para>
/// <para>
/// <c>RentReminderSentAt</c> is the dedupe: a lease is mailed once, then stamped,
/// so the next scan skips it even though it is still inside the window. A manual
/// notification from the admin tenant page stamps the same field, so the two
/// paths cannot both mail the same tenant.
/// </para>
/// </remarks>
public sealed class RentReminderWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<RentReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Rent reminder worker started.");

        // A short delay keeps the first scan off the request path during startup.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);

        do
        {
            await ScanAsync(stoppingToken);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));

        logger.LogInformation("Rent reminder worker stopped.");
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task ScanAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var queue = scope.ServiceProvider.GetRequiredService<IEmailQueue>();
            var frontendUrls = scope.ServiceProvider.GetRequiredService<IFrontendUrlBuilder>();

            var today = DateTime.UtcNow.Date;
            // Prefilter on the widest window the policy can choose; the exact window
            // is per-lease (short and long tenancies differ), so the final decision is
            // made in memory below.
            var windowEnd = today.AddDays(LeaseReminderPolicy.LongTermWindowDays);

            var candidates = await db.LeaseRecords
                .Include(l => l.Property)
                .Where(l => l.RentReminderSentAt == null
                            && l.TenantEmail != string.Empty
                            && (l.Status == TransactionStatus.Pending || l.Status == TransactionStatus.Active)
                            && l.LeaseEndDate >= today
                            && l.LeaseEndDate <= windowEnd)
                .ToListAsync(stoppingToken);

            if (candidates.Count == 0)
                return;

            var queued = 0;

            foreach (var lease in candidates)
            {
                var daysRemaining = (lease.LeaseEndDate.Date - today).Days;

                // A short let is reminded only a week out. A lease still further away
                // than its own window is left unstamped and picked up on a later scan.
                if (daysRemaining > LeaseReminderPolicy.WindowDays(lease.LeaseStartDate, lease.LeaseEndDate))
                    continue;

                var notice = EmailTemplates.RentExpiryNotice(
                    lease.TenantEmail,
                    lease.TenantName,
                    lease.Property?.Title ?? "your rented property",
                    lease.LeaseEndDate,
                    daysRemaining,
                    frontendUrls);

                queue.QueueEmail(logger, notice, "rent-expiry-notice");
                lease.RentReminderSentAt = DateTime.UtcNow;
                queued++;
            }

            if (queued == 0)
                return;

            await db.SaveChangesAsync(stoppingToken);

            logger.LogInformation("Queued {Count} rent-expiry reminder(s).", queued);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            // One bad scan must not kill the worker; the next tick retries.
            logger.LogError(ex, "Rent reminder scan failed.");
        }
    }
}
