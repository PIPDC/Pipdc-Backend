using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using PIPDC.Application.Email;

namespace PIPDC.Infrastructure.Email;

/// <summary>
/// Bounded in-memory <see cref="IEmailQueue"/> backed by a
/// <see cref="Channel{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a singleton so queued messages live for the lifetime of the
/// process rather than the request scope. The buffer is in memory, so unsent
/// messages are lost on restart or deploy.
/// </para>
/// <para>
/// That matches the behaviour these call sites had before the queue existed: a
/// failed send was caught and logged, and the email was lost. This is therefore
/// not a regression, but it does mean these emails are not a delivery
/// guarantee. Use a persistent outbox if transactional mail ever needs one.
/// </para>
/// <para>
/// Enqueue never blocks. When the buffer is full the message is dropped and
/// <see cref="EnqueueResult.Dropped"/> is returned, so a burst of traffic
/// cannot stall a request thread waiting for email capacity.
/// </para>
/// </remarks>
public sealed class EmailQueue(
    ILogger<EmailQueue> logger,
    int capacity = 500) : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel =
        Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });

    public EnqueueResult Enqueue(EmailMessage message, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return EnqueueResult.Dropped;

        if (_channel.Writer.TryWrite(message))
            return EnqueueResult.Accepted;

        logger.LogError(
            "Email queue is full at {Capacity}; dropped email to {Recipient} (subject: {Subject}). " +
            "Consider a persistent outbox if this recurs.",
            capacity, message.To, message.Subject);

        return EnqueueResult.Dropped;
    }

    /// <summary>
    /// Await the next queued message. Completes when the channel is drained.
    /// </summary>
    public ChannelReader<EmailMessage> Reader => _channel.Reader;
}
