// A single transient SMTP failure used to discard the outbound message for good.
// The worker caught the exception, logged it and moved on to the next message,
// and since the queue is in-memory and the message carried no attempt count
// there was nothing left to retry from. Almost everything on this path is a
// credential (the sign-in one-time code, the password-reset code, the badge
// activation), so a relay that hiccuped for a second produced a user who could
// never sign in, with only a log line to say why.
//
// These tests pin the two halves of the fix that can regress independently:
// which failures earn another attempt, and the cap that stops the retry from
// becoming a spin.
using System.Collections.Concurrent;
using System.Net.Sockets;
using MailKit.Net.Smtp;
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
public sealed class EmailSendRetryTests
{
    // The worker's budget, spelled out rather than reflected out of the private
    // const: if it changes, these tests should be re-read rather than silently
    // adapt to the new number.
    private const int MaxSendAttempts = 3;

    private const string Ops = "ops@simf.test";
    private const string Recipient = "visitor@simf.test";
    private const string Second = "other@simf.test";

    [Fact]
    public async Task A_transient_failure_is_retried_until_the_message_is_delivered()
    {
        var queue = new EmailQueue(NullLogger<EmailQueue>.Instance);
        var relay = new ScriptedRelay(new IOException("the relay reset the connection"), failures: 2);
        var worker = Build(queue, relay);
        await worker.StartAsync(CancellationToken.None);

        Assert.True(queue.Enqueue(new EmailMessage(Recipient, "Your code", "<p>1234</p>")));

        await WaitForAttemptsAsync(relay, MaxSendAttempts);
        await worker.StopAsync(CancellationToken.None);

        // Two failures, then the third attempt got through. Before the fix the
        // first failure was the end of the message.
        Assert.Equal(MaxSendAttempts, relay.CountTo(Recipient));
        Assert.Equal(1, relay.DeliveredTo(Recipient));
    }

    [Fact]
    public async Task A_transient_failure_that_never_clears_is_dropped_at_the_attempt_cap()
    {
        var queue = new EmailQueue(NullLogger<EmailQueue>.Instance);
        var relay = new ScriptedRelay(Reply(SmtpStatusCode.MailboxBusy), failures: int.MaxValue);
        var worker = Build(queue, relay);
        await worker.StartAsync(CancellationToken.None);

        Assert.True(queue.Enqueue(new EmailMessage(Recipient, "Your code", "<p>1234</p>")));

        await WaitForAttemptsAsync(relay, MaxSendAttempts);
        await worker.StopAsync(CancellationToken.None);

        // The cap is the only thing that stops it, and it has to be: a retry that
        // re-queued unconditionally would still be going, with the queue to
        // itself while it did.
        Assert.Equal(MaxSendAttempts, relay.CountTo(Recipient));
        Assert.Equal(0, relay.DeliveredTo(Recipient));
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public async Task A_permanently_refused_recipient_is_dropped_on_the_first_failure()
    {
        var queue = new EmailQueue(NullLogger<EmailQueue>.Instance);
        var relay = new ScriptedRelay(Reply(SmtpStatusCode.MailboxUnavailable), failures: int.MaxValue);
        var worker = Build(queue, relay);
        await worker.StartAsync(CancellationToken.None);

        Assert.True(queue.Enqueue(new EmailMessage(Recipient, "Your code", "<p>1234</p>")));

        await WaitForAttemptsAsync(relay, 1);
        await worker.StopAsync(CancellationToken.None);

        // "No such mailbox" is the relay's final answer, so the budget would be
        // spent on nothing: the address is wrong now and will be wrong on the
        // third attempt too. Dropped and logged, exactly as before the retry
        // existed.
        Assert.Equal(1, relay.CountTo(Recipient));
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public async Task An_unrecognised_failure_is_treated_as_permanent_and_is_not_retried()
    {
        var queue = new EmailQueue(NullLogger<EmailQueue>.Instance);
        var relay = new ScriptedRelay(
            new InvalidOperationException("relay unreachable"), failures: int.MaxValue);
        var worker = Build(queue, relay);
        await worker.StartAsync(CancellationToken.None);

        Assert.True(queue.Enqueue(new EmailMessage(Recipient, "Your code", "<p>1234</p>")));

        await WaitForAttemptsAsync(relay, 1);
        await worker.StopAsync(CancellationToken.None);

        // The keystone of the classification. Retrying on "I do not know" is how
        // a bounded budget still ends up spent on every fault in the system,
        // including the ones no attempt can clear.
        Assert.Equal(1, relay.CountTo(Recipient));
    }

    [Fact]
    public async Task A_retried_message_goes_to_the_back_of_the_queue()
    {
        var queue = new EmailQueue(NullLogger<EmailQueue>.Instance);
        var relay = new ScriptedRelay(new IOException("the relay reset the connection"), failures: 1);
        var worker = Build(queue, relay);

        // Both enqueued BEFORE the worker starts, so the order below is the order
        // the single consumer sees rather than a race with the producer.
        Assert.True(queue.Enqueue(new EmailMessage(Recipient, "Your code", "<p>1234</p>")));
        Assert.True(queue.Enqueue(new EmailMessage(Second, "Your code", "<p>5678</p>")));

        await worker.StartAsync(CancellationToken.None);
        await WaitForAttemptsAsync(relay, 2);
        await worker.StopAsync(CancellationToken.None);

        // The retry waits its turn behind the message that was already queued. At
        // the front it would be tried again immediately, and a recipient the
        // relay keeps deferring would hold up the sign-in codes behind it.
        Assert.Equal(new[] { Recipient, Second, Recipient }, relay.Attempts);
    }

    [Fact]
    public async Task The_outage_alert_still_fires_once_across_the_retries_of_one_message()
    {
        var queue = new EmailQueue(NullLogger<EmailQueue>.Instance);
        var relay = new ScriptedRelay(
            new IOException("the relay reset the connection"), failures: int.MaxValue);
        var worker = Build(queue, relay, alertRecipients: Ops);
        await worker.StartAsync(CancellationToken.None);

        Assert.True(queue.Enqueue(new EmailMessage(Recipient, "Your code", "<p>1234</p>")));

        await WaitForAttemptsAsync(relay, MaxSendAttempts);
        await worker.StopAsync(CancellationToken.None);

        // Retrying must not turn one outage into one alert per attempt. The alert
        // is sent inline on the only consumer, so amplifying it is precisely how
        // the queue stops draining faster than producers fill it.
        Assert.Equal(MaxSendAttempts, relay.CountTo(Recipient));
        Assert.Equal(1, relay.CountTo(Ops));
    }

    [Fact]
    public void A_4xx_reply_is_transient_and_a_5xx_reply_is_not()
    {
        // RFC 5321 already draws this line: 4xx is the SMTP way of saying
        // "try again later".
        Assert.True(SmtpEmailSender.IsTransientFailure(Reply(SmtpStatusCode.ServiceNotAvailable)));
        Assert.True(SmtpEmailSender.IsTransientFailure(Reply(SmtpStatusCode.MailboxBusy)));
        Assert.True(SmtpEmailSender.IsTransientFailure(Reply(SmtpStatusCode.InsufficientStorage)));

        // 5xx is the relay's final answer, so another attempt can only repeat it.
        Assert.False(SmtpEmailSender.IsTransientFailure(Reply(SmtpStatusCode.MailboxUnavailable)));
        Assert.False(SmtpEmailSender.IsTransientFailure(Reply(SmtpStatusCode.TransactionFailed)));
        Assert.False(
            SmtpEmailSender.IsTransientFailure(Reply(SmtpStatusCode.AuthenticationInvalidCredentials)));
    }

    [Fact]
    public void A_transport_fault_is_transient_because_it_says_nothing_about_the_recipient()
    {
        Assert.True(SmtpEmailSender.IsTransientFailure(new SocketException(10061)));
        Assert.True(SmtpEmailSender.IsTransientFailure(new IOException("connection reset")));
        Assert.True(SmtpEmailSender.IsTransientFailure(new TimeoutException("the socket timed out")));
        Assert.True(SmtpEmailSender.IsTransientFailure(new SmtpProtocolException("truncated reply")));
        Assert.True(
            SmtpEmailSender.IsTransientFailure(new MailKit.ServiceNotConnectedException("not connected")));
    }

    [Fact]
    public void A_configuration_fault_and_an_unrecognised_fault_are_both_permanent()
    {
        // Bad credentials and a certificate this client will not trust are
        // configuration: three more attempts produce three more of the same.
        Assert.False(SmtpEmailSender.IsTransientFailure(
            new MailKit.Security.AuthenticationException("authentication failed")));
        Assert.False(SmtpEmailSender.IsTransientFailure(
            new MailKit.Security.SslHandshakeException("the certificate is not trusted")));

        // An exception the transport does not recognise is not evidence of a
        // transient condition.
        Assert.False(
            SmtpEmailSender.IsTransientFailure(new InvalidOperationException("relay unreachable")));
        Assert.False(SmtpEmailSender.IsTransientFailure(new FormatException("an unparseable reply")));
    }

    [Fact]
    public void The_inner_exception_is_read_and_a_permanent_cause_anywhere_settles_it()
    {
        // MailKit wraps: a socket that went away mid-command arrives as a protocol
        // exception with the real fault underneath, so judging the outermost type
        // alone would miss it.
        Assert.True(SmtpEmailSender.IsTransientFailure(
            new SmtpProtocolException("unexpected end of stream", new IOException("connection reset"))));

        // The other direction is what stops a permanent fault from being retried
        // because its wrapper happens to look retryable.
        Assert.False(SmtpEmailSender.IsTransientFailure(
            new SmtpProtocolException(
                "handshake", new MailKit.Security.AuthenticationException("authentication failed"))));
    }

    private static EmailBackgroundService Build(
        EmailQueue queue, IEmailSender sender, string alertRecipients = "") =>
        new(queue,
            sender,
            Options.Create(new EmailOptions { FailureAlertRecipients = alertRecipients }),
            new NullHeartbeat(),
            NullLogger<EmailBackgroundService>.Instance);

    private static SmtpCommandException Reply(SmtpStatusCode status) =>
        new(SmtpErrorCode.UnexpectedStatusCode, status, $"{(int)status} from the relay");

    private static async Task WaitForAttemptsAsync(ScriptedRelay relay, int expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (relay.CountTo(Recipient) < expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        // Let any further attempt the worker would make actually land, so that a
        // test asserting "and no more than that" is asserting something.
        await Task.Delay(250);
    }

    /// <summary>
    /// A relay that fails the first N sends to the message recipient with a given
    /// fault and succeeds after that, recording every address it was handed in
    /// order so a test can count and sequence the attempts.
    /// </summary>
    private sealed class ScriptedRelay : IEmailSender
    {
        private readonly ConcurrentQueue<string> _attempts = new();
        private readonly ConcurrentQueue<string> _delivered = new();
        private readonly Exception _fault;
        private int _failuresLeft;

        public ScriptedRelay(Exception fault, int failures)
        {
            _fault = fault;
            _failuresLeft = failures;
        }

        /// <summary>Every address handed to the relay, in the order it was tried.</summary>
        public IReadOnlyCollection<string> Attempts => _attempts;

        public int CountTo(string address) =>
            _attempts.Count(attempt => string.Equals(attempt, address, StringComparison.Ordinal));

        public int DeliveredTo(string address) =>
            _delivered.Count(sent => string.Equals(sent, address, StringComparison.Ordinal));

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            _attempts.Enqueue(message.To);

            // Only the message recipient fails. The ops alert goes through the
            // same sender, and failing that too would be testing the alert path
            // rather than the retry.
            var fails = string.Equals(message.To, Recipient, StringComparison.Ordinal)
                && _failuresLeft > 0;
            if (!fails)
            {
                _delivered.Enqueue(message.To);
                return Task.CompletedTask;
            }

            _failuresLeft--;
            return Task.FromException(_fault);
        }
    }

    private sealed class NullHeartbeat : IWorkerHeartbeatRegistry
    {
        public void Register(string workerName, string description, TimeSpan expectedInterval) { }
        public void RecordSuccess(string workerName) { }
        public void RecordFailure(string workerName, string error) { }
        public WorkerStatusListResponse Snapshot() => throw new NotSupportedException();
    }
}
