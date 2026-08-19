// Tests: SIMF.Api.Tests/EmailFailureAlertTests.cs (outage alert behaviour)
// Tests: SIMF.Api.Tests/EmailSendRetryTests.cs (bounded retry of a transient failure)
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
/// configured, a send failure also dispatches a short notification email to the
/// operations distribution list so Support / IT see SMTP outages in real time
/// without having to tail the OperationLog. A recursive failure (the alert
/// ITSELF fails to send) is logged but never re-triggers another alert.</para>
///
/// <para>Only the FIRST failure of an outage alerts, and a successful send arms
/// it again. The alert is sent inline on the one and only consumer, so with two
/// ops recipients and a dead relay every dequeued message used to cost three
/// failing SMTP round trips instead of one — the queue then drained slower than
/// producers filled it, pinned at its bound, and refused everything after that.
/// The doc here used to call the alert "fire-and-forget" and claim it could not
/// amplify; it was neither.</para>
///
/// <para>A send that fails for a reason that could plausibly clear on its own
/// (a 4xx reply, a dropped socket, the send timing out) no longer loses the
/// message: it goes back on the BACK of the queue and is tried again, up to
/// <see cref="MaxSendAttempts"/> attempts in total. Anything the relay has
/// answered definitively, and anything the transport does not recognise, is
/// discarded on the first failure exactly as before, because retrying it cannot
/// change the answer and every attempt is a queue slot held away from the
/// messages behind it.</para>
/// </summary>
public sealed class EmailBackgroundService(
    EmailQueue queue,
    IEmailSender sender,
    IOptions<EmailOptions> options,
    IWorkerHeartbeatRegistry heartbeat,
    ILogger<EmailBackgroundService> logger) : BackgroundService
{
    /// <summary>How many times one message may be handed to the relay before it
    /// is discarded. Three is a delivery budget rather than a guess limit, so it
    /// is deliberately smaller than the code-entry caps elsewhere in SIMF: it only
    /// has to outlast the seconds-long blips a relay actually produces (a
    /// greylist, a restart, a reset socket), and every extra attempt is a queue
    /// slot held away from the messages behind it.</summary>
    private const int MaxSendAttempts = 3;

    private static readonly char[] RecipientDelimiters = [',', ';', ' ', '\t', '\r', '\n'];

    // One alert per outage, not one per message. Only ever touched by the single
    // consumer loop below, so it needs no synchronisation.
    private bool _outageAlreadyAlerted;

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
                // The relay answered, so the next failure is a NEW outage and
                // earns its own alert.
                _outageAlreadyAlerted = false;
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
                    "Failed to send email {Subject} to {Recipient} via SMTP {SmtpHost}:{SmtpPort} from {FromAddress} on attempt {Attempt} of {MaxAttempts}",
                    message.Subject, message.To, smtp.Host, smtp.Port, smtp.FromAddress,
                    message.AttemptCount + 1, MaxSendAttempts);
                RequeueOrDiscard(message, ex);

                // Unchanged, and still raised per failed ATTEMPT rather than per
                // discarded message: the alert exists so ops see an outage while it
                // is happening, and holding it back until a message exhausts its
                // attempts would delay the first alert by the whole retry budget.
                // The once-per-outage flag inside already collapses the extra
                // attempts into a single alert.
                await NotifyFailureAsync(message, ex, stoppingToken);
            }
        }
    }

    /// <summary>
    /// Puts a failed message back at the BACK of the queue when another attempt
    /// could plausibly succeed, and lets it go otherwise. Letting it go is what
    /// this worker has always done with a failed send; the bounded retry is what
    /// stops a relay hiccup from having the same outcome as a wrong address.
    /// </summary>
    private void RequeueOrDiscard(EmailMessage message, Exception cause)
    {
        // The classification lives on the SMTP sender because the exceptions being
        // judged are MailKit's and this worker only holds an IEmailSender. A refused
        // recipient, a rejected sender or bad credentials fail identically every
        // time, so retrying one only delays the drop while holding a queue slot away
        // from a message that could have been delivered.
        if (!SmtpEmailSender.IsTransientFailure(cause))
        {
            return;
        }

        var attemptsMade = message.AttemptCount + 1;
        if (attemptsMade >= MaxSendAttempts)
        {
            logger.LogError(
                "Giving up on the email {Subject} to {Recipient} after {Attempts} attempts; the message is discarded.",
                message.Subject, message.To, attemptsMade);
            return;
        }

        // The BACK of the queue, never the front: everything already waiting gets
        // its turn before this message is tried again, so one recipient the relay
        // keeps deferring cannot hold up the sign-in codes behind it. A full queue
        // refuses the write and logs its own error, in which case the retry is
        // discarded exactly like one that ran out of attempts.
        if (queue.Enqueue(message with { AttemptCount = attemptsMade }))
        {
            logger.LogWarning(
                "Re-queued the email {Subject} to {Recipient} for attempt {NextAttempt} of {MaxAttempts} after a transient send failure.",
                message.Subject, message.To, attemptsMade + 1, MaxSendAttempts);
        }
    }

    private async Task NotifyFailureAsync(
        EmailMessage failed, Exception cause, CancellationToken cancellationToken)
    {
        if (_outageAlreadyAlerted) { return; }

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

        // Set BEFORE the sends: they go through the same relay that just failed,
        // so this must hold even when every one of them fails too.
        _outageAlreadyAlerted = true;

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
