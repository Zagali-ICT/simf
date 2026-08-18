using System.Collections.Concurrent;
using SIMF.Application.Email;

namespace SIMF.Api.Tests;

/// <summary>
/// D-751 — a test double for <see cref="IEmailQueue"/> that records the enqueued
/// messages synchronously instead of handing them to the async background sender.
/// Used by <see cref="BulkBadgeEmailApiFactory"/> so a test can assert on the
/// exact message (and its attachments) an endpoint enqueued, deterministically.
/// </summary>
public sealed class FakeEmailQueue : IEmailQueue
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    /// <summary>The messages enqueued so far (shared across the fixture's tests).</summary>
    public IReadOnlyCollection<EmailMessage> Messages => _messages;

    /// <summary>Always accepts: the fake is unbounded, so it never exercises the
    /// real queue's "refused because full" path (see EmailQueueDropTests for that).</summary>
    public bool Enqueue(EmailMessage message)
    {
        _messages.Enqueue(message);
        return true;
    }

    /// <summary>The fake records synchronously, so nothing is ever buffered awaiting
    /// send — always 0, matching the real queue's "not yet sent" semantic. Keeps the
    /// broadcast worker's pacing check from ever tripping in tests.</summary>
    public int PendingCount => 0;
}
