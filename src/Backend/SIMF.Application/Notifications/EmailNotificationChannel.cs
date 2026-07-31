// Tests: SIMF.Api.Tests/NotificationChannelTests.cs
//        SIMF.Api.Tests/NotificationLifecycleTests.cs (P13 — trigger-by-trigger)
using Microsoft.Extensions.Logging;
using SIMF.Application.Email;
using SIMF.Application.IdentityAccess.Abstractions;

namespace SIMF.Application.Notifications;

/// <summary>
/// `sms-whatsapp-channels` — the email channel. Handles only the requests that
/// opted in via <see cref="NotificationRequest.SendEmail"/>, and quietly skips a
/// user with no address on file rather than failing the dispatch: the in-app row
/// (written first, <see cref="InAppNotificationChannel"/>) is the delivery that
/// matters, email is best-effort on top.
///
/// <para>The send logic is lifted verbatim out of <c>NotificationDispatcher</c> —
/// this is a move, not a rewrite.</para>
/// </summary>
internal sealed class EmailNotificationChannel(
    IUserAccountStore accounts,
    IEmailQueue emailQueue,
    ILogger<EmailNotificationChannel> logger) : INotificationChannel
{
    public string Name => "email";

    public int Order => 10;

    public bool ShouldHandle(NotificationRequest request) => request.SendEmail;

    public async Task SendAsync(
        NotificationRequest request, CancellationToken cancellationToken = default)
    {
        var user = await accounts.FindByIdAsync(request.UserId, cancellationToken);
        if (user?.Email is null)
        {
            logger.LogWarning(
                "Notification {Kind} for {UserId}: email skipped — no address on file.",
                request.Kind, request.UserId);
            return;
        }

        // Subject = the English title; the user's culture decides the body. A
        // culture-aware queue / per-user language preference is a P13 follow-up.
        var body = request.PreRenderedEmailHtml
            ?? $"<p>{System.Net.WebUtility.HtmlEncode(request.Body)}</p>";
        emailQueue.Enqueue(new EmailMessage(user.Email, request.Title, body));
    }
}
