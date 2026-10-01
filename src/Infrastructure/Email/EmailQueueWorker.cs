using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PIPDC.Application.Email;

namespace PIPDC.Infrastructure.Email;

/// <summary>
/// Drains <see cref="EmailQueue"/> and sends each message on a dedicated scope.
/// </summary>
/// <remarks>
/// <see cref="IEmailService"/> is registered as scoped because it depends on
/// options and a logger, so the worker creates a scope per message rather than
/// capturing a scoped service in its constructor.
///
/// A send failure is logged and the message is dropped. There is no retry: the
/// upstream calls are notification mail that is already best-effort, and a
/// persistent retry would need a durable queue. A poison message therefore
/// cannot stall the worker.
/// </remarks>
public sealed class EmailQueueWorker(
    IServiceScopeFactory scopeFactory,
    EmailQueue queue,
    ILogger<EmailQueueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Email queue worker started.");

        try
        {
            await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await DeliverAsync(message, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            // A failure here means the reader itself broke, not one message.
            // Log and exit rather than taking the host process down.
            logger.LogError(ex, "Email queue worker stopped unexpectedly.");
        }

        logger.LogInformation("Email queue worker stopped.");
    }

    private async Task DeliverAsync(EmailMessage message, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            await emailService.SendAsync(message, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down; stop draining.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Background email delivery failed for {Recipient} (subject: {Subject}).",
                message.To, message.Subject);
        }
    }
}
