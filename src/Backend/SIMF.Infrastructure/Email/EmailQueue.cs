using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SIMF.Application.Email;

namespace SIMF.Infrastructure.Email;

/// <summary>
/// An in-process email queue backed by a bounded channel. Producers call
/// <see cref="Enqueue"/>; <see cref="EmailBackgroundService"/> drains the
/// <see cref="Reader"/>. The bound caps memory if the sender stalls; a dropped
/// message is logged rather than lost silently.
/// </summary>
public sealed class EmailQueue(ILogger<EmailQueue> logger) : IEmailQueue
{
    private const int Capacity = 1000;

    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropWrite });

    /// <summary>The read side of the queue, drained by the background sender.</summary>
    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public void Enqueue(EmailMessage message)
    {
        if (!_channel.Writer.TryWrite(message))
        {
            logger.LogError(
                "The email queue is full ({Capacity} messages); the message to {Recipient} was dropped.",
                Capacity,
                message.To);
        }
    }
}
