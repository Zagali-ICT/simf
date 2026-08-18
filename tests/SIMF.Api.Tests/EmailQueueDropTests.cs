// Regression tests for the two silent-failure defects on the outbound email
// path. Both mattered because almost everything SIMF sends through this path is
// a credential: the sign-in one-time code, the password-reset code, the
// email-verification code, the badge activation and the admin invite.
//
//   1. The bounded queue was built with BoundedChannelFullMode.DropWrite, whose
//      contract is that the written item is discarded and TryWrite still returns
//      TRUE. A full queue therefore threw away credential emails with no log
//      line, no audit row, and success reported to the caller.
//   2. SmtpEmailSender connected with StartTlsWhenAvailable, which continues in
//      cleartext when the server does not advertise STARTTLS, and then sent AUTH
//      and the codes over that session.
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Common.Enums;
using SIMF.Infrastructure.Email;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class EmailQueueDropTests
{
    // The real queue's bound, spelled out rather than reflected out of the
    // private const: if the capacity changes, this test should be re-read rather
    // than silently adapt to the new number.
    private const int Capacity = 1000;

    private const string Overflow = "overflow@simf.test";

    private static EmailMessage Message(string to = "someone@simf.test") =>
        new(to, "Subject", "<p>body</p>");

    private static EmailQueue NewQueue() => new(NullLogger<EmailQueue>.Instance);

    [Fact]
    public void Enqueue_accepts_up_to_capacity_and_then_reports_the_refusal()
    {
        var queue = NewQueue();

        var accepted = 0;
        for (var i = 0; i < Capacity; i++)
        {
            if (queue.Enqueue(Message()))
            {
                accepted++;
            }
        }

        Assert.Equal(Capacity, accepted);

        // The defect: this returned TRUE while discarding the message.
        Assert.False(queue.Enqueue(Message(Overflow)));
        Assert.Equal(Capacity, queue.PendingCount);
    }

    [Fact]
    public void A_refused_message_is_never_buffered_for_delivery()
    {
        var queue = NewQueue();
        for (var i = 0; i < Capacity; i++)
        {
            queue.Enqueue(Message());
        }

        queue.Enqueue(Message(Overflow));

        // A "refusal" that still queued the message, or that evicted an already
        // accepted one to make room, would be a different bug with the same
        // symptom. Drain the channel and check neither happened.
        var drained = new List<EmailMessage>();
        while (queue.Reader.TryRead(out var message))
        {
            drained.Add(message);
        }

        Assert.Equal(Capacity, drained.Count);
        Assert.DoesNotContain(drained, message => message.To == Overflow);
    }

    [Fact]
    public async Task TryEnqueueAsync_audits_EmailEnqueueFailed_when_the_queue_refuses()
    {
        var audit = new RecordingAuditLog();
        var userId = Guid.NewGuid();

        await new RefusingEmailQueue().TryEnqueueAsync(
            Message("victim@simf.test"),
            purpose: "PasswordReset",
            subjectEmail: "victim@simf.test",
            subjectUserId: userId,
            auditLog: audit,
            logger: NullLogger.Instance);

        // Before the fix the wrapper only caught exceptions, and a refusing queue
        // does not throw, so a full queue audited nothing at all.
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditEvents.EmailEnqueueFailed, entry.EventType);
        Assert.Equal(AuditOutcome.Failure, entry.Outcome);
        Assert.Equal("victim@simf.test", entry.SubjectEmail);
        Assert.Equal(userId, entry.SubjectUserId!.Value);

        // The detail names both the purpose and which of the two failure modes it
        // was, so the audit trail separates "queue full" from "queue threw".
        Assert.Contains("PasswordReset", entry.Detail!, StringComparison.Ordinal);
        Assert.Contains("QueueFull", entry.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryEnqueueAsync_audits_nothing_when_the_queue_accepts()
    {
        var audit = new RecordingAuditLog();

        await new FakeEmailQueue().TryEnqueueAsync(
            Message(),
            purpose: "SignInOtp",
            subjectEmail: "someone@simf.test",
            subjectUserId: null,
            auditLog: audit,
            logger: NullLogger.Instance);

        Assert.Empty(audit.Entries);
    }

    [Theory]
    [InlineData(587)]
    [InlineData(25)]
    [InlineData(2525)]
    public void Submission_ports_require_STARTTLS_rather_than_preferring_it(int port)
    {
        var chosen = SmtpEmailSender.SecureOptionsForPort(port);

        Assert.Equal(SecureSocketOptions.StartTls, chosen);

        // Named explicitly because these are the two values that would reintroduce
        // the downgrade: an attacker who strips STARTTLS from the EHLO response
        // gets a cleartext session carrying AUTH and the one-time codes.
        Assert.NotEqual(SecureSocketOptions.StartTlsWhenAvailable, chosen);
        Assert.NotEqual(SecureSocketOptions.None, chosen);
    }

    [Fact]
    public void Port_465_connects_with_implicit_TLS()
    {
        // 465 is TLS from the first byte and has no plaintext phase in which a
        // STARTTLS command could be issued, so requiring STARTTLS there would fail
        // every send rather than secure it.
        Assert.Equal(
            SecureSocketOptions.SslOnConnect,
            SmtpEmailSender.SecureOptionsForPort(465));
    }

    private sealed class RefusingEmailQueue : IEmailQueue
    {
        public bool Enqueue(EmailMessage message) => false;

        public int PendingCount => 0;
    }

    private sealed class RecordingAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
