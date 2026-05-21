using System.Threading.Channels;
using SIMF.Application.Email;

namespace SIMF.Infrastructure.Email;

/// <summary>
/// An in-process email queue backed by an unbounded channel. Producers call
/// <see cref="Enqueue"/>; <see cref="EmailBackgroundService"/> drains the
/// <see cref="Reader"/>.
/// </summary>
public sealed class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>();

    /// <summary>The read side of the queue, drained by the background sender.</summary>
    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public void Enqueue(EmailMessage message) => _channel.Writer.TryWrite(message);
}
