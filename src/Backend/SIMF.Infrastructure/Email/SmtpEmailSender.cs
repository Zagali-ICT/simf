// Tests: SIMF.Api.Tests/EmailQueueDropTests.cs (the transport-security choice)
// Tests: SIMF.Api.Tests/EmailSendRetryTests.cs (which failures are worth retrying)
using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SIMF.Application.Email;
using SIMF.Common.Options;

namespace SIMF.Infrastructure.Email;

/// <summary>Sends email over SMTP using MailKit.</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    /// <summary>The conventional implicit-TLS submission port. A server listening
    /// here expects the TLS handshake BEFORE the SMTP greeting, so there is no
    /// plaintext phase in which a STARTTLS command could be issued.</summary>
    private const int ImplicitTlsPort = 465;

    /// <summary>Per-socket-operation timeout, milliseconds. MailKit defaults to
    /// two minutes, which is the whole cost of an unreachable relay: the single
    /// consumer in <see cref="EmailBackgroundService"/> awaits every send inline,
    /// so a black-holed host stalls the queue for two minutes per message while
    /// producers keep filling it. A value rather than a configuration key, so
    /// there is no new setting to keep in step across appsettings and the
    /// set-env scripts.</summary>
    private const int SocketTimeoutMs = 30_000;

    /// <summary>
    /// The transport security to connect with. Neither branch can produce an
    /// unencrypted session.
    ///
    /// <para>
    /// This used to be <c>StartTlsWhenAvailable</c>, which is a downgrade waiting
    /// to happen: when the server's EHLO response omits STARTTLS, MailKit silently
    /// continues in CLEARTEXT, and SIMF then sent the SMTP username and password
    /// over it, followed by sign-in one-time codes, password-reset codes, badge
    /// activations and admin invites. An active attacker does not need to break
    /// anything to trigger that — stripping the STARTTLS capability out of the
    /// EHLO response is the textbook downgrade, and "when available" makes
    /// encryption the attacker's choice rather than ours.
    /// </para>
    ///
    /// <para>
    /// So the choice fails closed instead. <c>StartTls</c> REQUIRES the upgrade
    /// and throws when the server will not offer it; <c>SslOnConnect</c> is TLS
    /// from the first byte. A misconfigured or hostile relay now produces a loud
    /// send failure — logged, heartbeat-recorded and alert-emailed by
    /// <see cref="EmailBackgroundService"/>, which catches per message and keeps
    /// draining — rather than a quiet plaintext credential leak.
    /// </para>
    ///
    /// <para>
    /// The port picks the mode because <see cref="EmailOptions"/> carries no
    /// transport flag and inventing one would add a key that has to be kept in
    /// step across appsettings and every set-env script. 465 is implicit TLS by
    /// long-standing convention; 587 and 25 are the STARTTLS submission ports.
    /// </para>
    /// </summary>
    /// <param name="port">The configured SMTP port.</param>
    public static SecureSocketOptions SecureOptionsForPort(int port) =>
        port == ImplicitTlsPort
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

    /// <summary>
    /// Whether a failed send is worth another attempt, or is the relay's final
    /// answer. <see cref="EmailBackgroundService"/> asks this before it puts a
    /// message back on the queue.
    ///
    /// <para>The taxonomy lives with the transport that raises it rather than with
    /// the worker that catches it: these are MailKit exceptions, and the worker
    /// holds an <see cref="IEmailSender"/> without knowing what is behind it. It is
    /// a static, like <see cref="SecureOptionsForPort"/>, so the classification can
    /// be tested directly without an SMTP server to fail against.</para>
    ///
    /// <para>An unrecognised failure counts as permanent, which is the keystone of
    /// the scheme rather than an oversight: an exception this transport does not
    /// recognise is not evidence of a transient condition, and retrying on "I do
    /// not know" is how a queue starts to spin against a fault that will never
    /// clear.</para>
    /// </summary>
    /// <param name="failure">The exception thrown by the failed send.</param>
    public static bool IsTransientFailure(Exception failure)
    {
        var sawTransient = false;

        // MailKit routinely wraps the deciding fault, so the outermost type is not
        // always the one that answers the question: a socket that went away
        // mid-command surfaces as a protocol exception with the IOException
        // underneath. A permanent cause found ANYWHERE in the chain settles it,
        // because no number of further attempts can change that answer.
        for (Exception? error = failure; error is not null; error = error.InnerException)
        {
            if (IsPermanentCause(error))
            {
                return false;
            }

            sawTransient |= IsTransientCause(error);
        }

        return sawTransient;
    }

    /// <summary>A failure that would repeat identically however many times the
    /// message were handed to the relay again.</summary>
    private static bool IsPermanentCause(Exception error)
    {
        // RFC 5321 splits the reply codes for exactly this purpose: 4xx means "try
        // again later" and 5xx means "do not". A 5xx is the relay saying the
        // mailbox does not exist, this sender may not relay, or the message is
        // refused outright.
        if (error is SmtpCommandException command)
        {
            var status = (int)command.StatusCode;
            return status >= 500;
        }

        // Wrong credentials, a mechanism the server will not accept, or a
        // certificate this client will not trust. All three are configuration, so
        // further attempts only delay the drop and hold a queue slot while they do.
        // Judged before the transient kinds below, because a handshake failure
        // carries the underlying stream fault as its inner exception and would
        // otherwise read as a retryable blip.
        return error is MailKit.Security.AuthenticationException
            || error is SslHandshakeException;
    }

    /// <summary>A failure that a later attempt could plausibly get past.</summary>
    private static bool IsTransientCause(Exception error)
    {
        // A 4xx reply is the SMTP way of saying "not now": the mailbox is busy, the
        // store is full, the service is restarting, the relay is greylisting a
        // sender it has not seen before.
        if (error is SmtpCommandException command)
        {
            var status = (int)command.StatusCode;
            return status is >= 400 and < 500;
        }

        // A truncated or garbled reply, a connection that dropped mid-command, a
        // host that is unreachable or refusing, a stream fault under TLS, and the
        // socket timeout above expiring. Every one of these is the transport
        // rather than the recipient, and the recipient is what a retry cannot fix.
        return error is SmtpProtocolException
            || error is MailKit.ServiceNotConnectedException
            || error is SocketException
            || error is IOException
            || error is TimeoutException;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;

        // Attachments (e.g. the bulk-badge QR ZIP) ride the same
        // BodyBuilder; without any, this is identical to the previous single-part
        // HTML body.
        var builder = new BodyBuilder { HtmlBody = message.HtmlBody };
        if (message.Attachments is { Count: > 0 })
        {
            foreach (var attachment in message.Attachments)
            {
                builder.Attachments.Add(
                    attachment.FileName,
                    attachment.Content,
                    ContentType.Parse(attachment.ContentType));
            }
        }
        mime.Body = builder.ToMessageBody();

        using var client = new SmtpClient { Timeout = SocketTimeoutMs };
        await client.ConnectAsync(
            settings.Host,
            settings.Port,
            SecureOptionsForPort(settings.Port),
            cancellationToken);
        if (!string.IsNullOrEmpty(settings.User))
        {
            await client.AuthenticateAsync(settings.User, settings.Password, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
