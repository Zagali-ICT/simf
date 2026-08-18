// Tests: SIMF.Api.Tests/EmailQueueDropTests.cs
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SIMF.Application.Email;

namespace SIMF.Infrastructure.Email;

/// <summary>
/// An in-process email queue backed by a bounded channel. Producers call
/// <see cref="Enqueue"/>; <see cref="EmailBackgroundService"/> drains the
/// <see cref="Reader"/>. The bound caps memory if the sender stalls, and a
/// message the bound refuses is reported to the caller rather than lost.
///
/// <para>
/// The full-mode is <see cref="BoundedChannelFullMode.Wait"/> and that choice is
/// load-bearing. This class previously used <c>DropWrite</c>, whose contract is
/// that the item being written is discarded and <c>TryWrite</c> still returns
/// TRUE. The "queue is full" branch below was therefore unreachable: on a full
/// queue SIMF dropped a sign-in code, a password-reset code or an admin invite
/// with no log line, no audit row and a success returned to the caller. Under
/// <c>Wait</c>, <c>TryWrite</c> instead REFUSES the write and returns false, which
/// is the same drop made visible. Nothing actually waits — this type only ever
/// calls <c>TryWrite</c>, never <c>WriteAsync</c>, so the mode changes what a full
/// queue REPORTS and not how it behaves.
/// </para>
/// </summary>
public sealed class EmailQueue(ILogger<EmailQueue> logger) : IEmailQueue
{
    private const int Capacity = 1000;

    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.Wait });

    /// <summary>The read side of the queue, drained by the background sender.</summary>
    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    /// <summary>The messages currently buffered (bounded channels expose a live
    /// count). Used by the broadcast worker to pace its fan-out under
    /// <see cref="Capacity"/>.</summary>
    public int PendingCount => _channel.Reader.Count;

    public bool Enqueue(EmailMessage message)
    {
        if (_channel.Writer.TryWrite(message))
        {
            return true;
        }

        // Deliberately Error, not Warning: the recipient is now waiting for an
        // email that will never arrive, and the only other trace is the audit row
        // the caller writes.
        logger.LogError(
            "The email queue is full ({Capacity} messages); the message to {Recipient} was refused and will NOT be sent.",
            Capacity,
            message.To);
        return false;
    }
}
