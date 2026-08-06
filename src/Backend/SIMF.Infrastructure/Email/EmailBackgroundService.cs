using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Email;
using SIMF.Application.Operations;
using SIMF.Common.Options;

namespace SIMF.Infrastructure.Email;

/// <summary>
/// Drains the <see cref="EmailQueue"/> and sends each message. A failed send is
/// logged and does not stop the worker, so one bad address never blocks the
/// queue.
///
/// <para>When <see cref="EmailOptions.FailureAlertRecipients"/> is
/// configured, every send failure also dispatches a short notification email
/// to the operations distribution list so Support / IT see SMTP outages in
/// real time without having to tail the OperationLog. The alert email is
/// fire-and-forget; a recursive failure (the alert ITSELF fails to send)
/// is logged but does NOT re-trigger another alert — otherwise a sustained
/// SMTP outage would amplify the queue.</para>
/// </summary>
public sealed class EmailBackgroundService(
    EmailQueue queue,
    IEmailSender sender,
    IOptions<EmailOptions> options,
    IWorkerHeartbeatRegistry heartbeat,
    ILogger<EmailBackgroundService> logger) : BackgroundService
{
    private static readonly char[] RecipientDelimiters = [',', ';', ' ', '\t', '\r', '\n'];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        heartbeat.Register(
            nameof(EmailBackgroundService), "Sends queued outbound emails.", TimeSpan.Zero);
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await sender.SendAsync(message, stoppingToken);
                heartbeat.RecordSuccess(nameof(EmailBackgroundService));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Log the SMTP endpoint that was attempted (host / port / from —
                // never the user or password) so ops can tell a wrong host/port
                // or unauthorised From address apart from a real outage straight
                // from the log, without cross-referencing the config. A
                // "code not received" report is almost always this transport
                // step, since the code itself is already persisted + enqueued.
                heartbeat.RecordFailure(nameof(EmailBackgroundService), ex.Message);
                var smtp = options.Value;
                logger.LogError(ex,
                    "Failed to send email {Subject} to {Recipient} via SMTP {SmtpHost}:{SmtpPort} from {FromAddress}",
                    message.Subject, message.To, smtp.Host, smtp.Port, smtp.FromAddress);
                await NotifyFailureAsync(message, ex, stoppingToken);
            }
        }
    }

    private async Task NotifyFailureAsync(
        EmailMessage failed, Exception cause, CancellationToken cancellationToken)
    {
        var recipientsRaw = options.Value.FailureAlertRecipients;
        if (string.IsNullOrWhiteSpace(recipientsRaw)) { return; }

        var recipients = recipientsRaw
            .Split(RecipientDelimiters, StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (recipients.Length == 0) { return; }

        var subject = $"[SIMF] Email send failure to {failed.To}";
        var body =
            $"<p>An outbound email failed to send.</p>"
            + $"<p><strong>Recipient:</strong> {System.Net.WebUtility.HtmlEncode(failed.To)}<br/>"
            + $"<strong>Subject:</strong> {System.Net.WebUtility.HtmlEncode(failed.Subject)}<br/>"
            + $"<strong>Exception:</strong> {System.Net.WebUtility.HtmlEncode(cause.GetType().Name)}: "
            + $"{System.Net.WebUtility.HtmlEncode(cause.Message)}</p>"
            + "<p>Check the SIMF application logs for the full stack trace and the OperationLog "
            + "for the matching audit row.</p>";

        foreach (var recipient in recipients)
        {
            try
            {
                await sender.SendAsync(new EmailMessage(recipient, subject, body), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception alertEx)
            {
                // Do NOT recurse — if the alert path itself fails, log and
                // move on. Otherwise a sustained SMTP outage would amplify
                // a single failure into one alert per recipient per retry.
                logger.LogError(alertEx,
                    "Email failure-alert to {AlertRecipient} also failed", recipient);
            }
        }
    }
}
