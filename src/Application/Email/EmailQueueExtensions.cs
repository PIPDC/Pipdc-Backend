using Microsoft.Extensions.Logging;

namespace PIPDC.Application.Email;

/// <summary>
/// Convenience wrapper for queuing email from application services.
/// </summary>
/// <remarks>
/// Email is a side effect, so a failure to enqueue must never surface as a failed
/// business operation. This wrapper swallows enqueue-level faults and logs them,
/// matching the best-effort behaviour the direct <c>IEmailService</c> call sites
/// had before the queue existed.
/// </remarks>
public static class EmailQueueExtensions
{
    /// <summary>
    /// Queues an email for background delivery without throwing.
    /// </summary>
    /// <param name="queue">The queue.</param>
    /// <param name="logger">Logger used to report drops and enqueue faults.</param>
    /// <param name="message">The message to deliver.</param>
    /// <param name="context">
    /// Short description used in logs, for example <c>new-enquiry</c>, so a
    /// failure can be traced back to its origin.
    /// </param>
    /// <param name="ct">
    /// Cancellation token for the enqueue operation only. Do not pass a
    /// token that is cancelled once the request completes.
    /// </param>
    public static void QueueEmail(
        this IEmailQueue queue,
        ILogger logger,
        EmailMessage message,
        string context,
        CancellationToken ct = default)
    {
        try
        {
            if (queue.Enqueue(message, ct) == EnqueueResult.Dropped)
            {
                logger.LogWarning(
                    "Email queue full; dropped {Context} email to {Recipient}.",
                    context, message.To);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Failed to enqueue {Context} email for {Recipient}.",
                context, message.To);
        }
    }
}
