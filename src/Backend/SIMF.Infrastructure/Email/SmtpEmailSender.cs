// Tests: SIMF.Api.Tests/EmailQueueDropTests.cs (the transport-security choice)
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

        using var client = new SmtpClient();
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
