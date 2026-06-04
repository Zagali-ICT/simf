using System.Collections.Concurrent;
using SIMF.Application.Email;

namespace SIMF.Api.Tests;

/// <summary>
/// A test double for <see cref="IEmailSender"/> — it records messages instead
/// of sending them, so integration tests never touch a real SMTP server.
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    /// <summary>The messages handed to the sender so far.</summary>
    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
