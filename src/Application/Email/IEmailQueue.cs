namespace PIPDC.Application.Email;

/// <summary>
/// Accepts outbound email for delivery by the background worker.
/// </summary>
/// <remarks>
/// Enqueueing is intentionally fire-and-forget: it never performs network IO and
/// never throws for delivery problems, so callers keep their current
/// best-effort semantics and never need to await a send.
///
/// Implementations are in-memory and therefore do not survive a process restart.
/// Use <see cref="EnqueueResult.Dropped"/> to detect that a message was discarded
/// because the queue was full, so callers can log or surface the loss.
/// </remarks>
public interface IEmailQueue
{
    /// <summary>
    /// Queues a message for background delivery. Returns immediately.
    /// </summary>
    /// <param name="message">The message to deliver.</param>
    /// <param name="ct">
    /// Cancellation token for the enqueue operation only, not for delivery.
    /// </param>
    EnqueueResult Enqueue(EmailMessage message, CancellationToken ct = default);
}

public enum EnqueueResult
{
    /// <summary>The message was accepted for delivery.</summary>
    Accepted = 0,

    /// <summary>
    /// The queue was full, so the message was discarded. This is a real loss and
    /// should be logged by the caller.
    /// </summary>
    Dropped = 1
}
