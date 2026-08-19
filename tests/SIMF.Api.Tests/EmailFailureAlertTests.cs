// The outbound-email worker sends its ops failure alert INLINE on the single
// consumer that drains the queue. Alerting per failed message therefore
// multiplied a relay outage: with two ops recipients every dequeued message cost
// three failing SMTP round trips instead of one, so the queue drained slower
// than producers filled it and pinned at its bound, after which every further
// message (a sign-in code, a reset code, an invite) was refused.
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SIMF.Application.Email;
using SIMF.Application.Operations;
using SIMF.Common.Options;
using SIMF.Contracts.Ops;
using SIMF.Infrastructure.Email;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class EmailFailureAlertTests
{
    private const string Ops1 = "ops1@simf.test";
    private const string Ops2 = "ops2@simf.test";
    private const string Recipient = "visitor@simf.test";

    /// <summary>Fails every send, like a dead relay, and records who it was for.</summary>
    private sealed class DeadRelaySender : IEmailSender
    {
        private readonly ConcurrentQueue<string> _attempts = new();
        private readonly bool[] _failures;

        public DeadRelaySender(params bool[] failures) => _failures = failures;

        public IReadOnlyCollection<string> Attempts => _attempts;

        public int CountTo(string address) =>
            _attempts.Count(a => string.Equals(a, address, StringComparison.Ordinal));

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            _attempts.Enqueue(message.To);
            var index = _attempts.Count - 1;
            var fails = index >= _failures.Length || _failures[index];
            return fails
                ? Task.FromException(new InvalidOperationException("relay unreachable"))
                : Task.CompletedTask;
        }
    }

    private sealed class NullHeartbeat : IWorkerHeartbeatRegistry
    {
        public void Register(string workerName, string description, TimeSpan expectedInterval) { }
        public void RecordSuccess(string workerName) { }
        public void RecordFailure(string workerName, string error) { }
        public WorkerStatusListResponse Snapshot() => throw new NotSupportedException();
    }

    private static EmailBackgroundService Build(EmailQueue queue, IEmailSender sender) =>
        new(queue,
            sender,
            Options.Create(new EmailOptions { FailureAlertRecipients = $"{Ops1}, {Ops2}" }),
            new NullHeartbeat(),
            NullLogger<EmailBackgroundService>.Instance);

    private static async Task DrainAsync(DeadRelaySender sender, int expectedMessageAttempts)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (sender.CountTo(Recipient) < expectedMessageAttempts && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        // Let any further alert the worker would send actually land.
        await Task.Delay(250);
    }

    [Fact]
    public async Task An_outage_alerts_ops_once_not_once_per_queued_message()
    {
        var queue = new EmailQueue(NullLogger<EmailQueue>.Instance);
        var sender = new DeadRelaySender();
        var worker = Build(queue, sender);
        await worker.StartAsync(CancellationToken.None);

        for (var i = 0; i < 3; i++)
        {
            Assert.True(queue.Enqueue(new EmailMessage(Recipient, "Your code", "<p>1234</p>")));
        }

        await DrainAsync(sender, expectedMessageAttempts: 3);
        await worker.StopAsync(CancellationToken.None);

        // Three real sends were attempted, and the outage cost exactly one alert
        // per ops recipient — not one per message per recipient.
        Assert.Equal(3, sender.CountTo(Recipient));
        Assert.Equal(1, sender.CountTo(Ops1));
        Assert.Equal(1, sender.CountTo(Ops2));
    }

    [Fact]
    public async Task A_successful_send_arms_the_alert_again()
    {
        var queue = new EmailQueue(NullLogger<EmailQueue>.Instance);
        // fail (alerts twice), then the two alert sends succeed, then a success,
        // then fail again — the second outage must alert again.
        var sender = new DeadRelaySender(true, false, false, false, true);
        var worker = Build(queue, sender);
        await worker.StartAsync(CancellationToken.None);

        for (var i = 0; i < 3; i++)
        {
            Assert.True(queue.Enqueue(new EmailMessage(Recipient, "Your code", "<p>1234</p>")));
        }

        await DrainAsync(sender, expectedMessageAttempts: 3);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(3, sender.CountTo(Recipient));
        Assert.Equal(2, sender.CountTo(Ops1));
        Assert.Equal(2, sender.CountTo(Ops2));
    }
}
