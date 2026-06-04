// Tests: SIMF.Api.Tests/NotificationTests.cs (round-trips)
//        SIMF.Api.Tests/NotificationLifecycleTests.cs (P13 — trigger-by-trigger)
using Microsoft.Extensions.Logging;
using SIMF.Application.Email;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Domain.Notifications;

namespace SIMF.Application.Notifications;

/// <summary>
/// Writes the in-app notification row + (when
/// <see cref="NotificationRequest.SendEmail"/> is true) queues an email
/// (P12 — D-053). The DB row write always lands; an email failure
/// never poisons the in-app delivery — they are independent channels.
///
/// <para>R4 — D-095: moved from <c>SIMF.Infrastructure.Notifications</c> to
/// the Application layer. Persistence is delegated to
/// <see cref="INotificationRepository"/>; this class only owns the
/// orchestration (build the entity, persist, optionally enqueue an email).</para>
/// </summary>
internal sealed class NotificationDispatcher(
    INotificationRepository notifications,
    IUserAccountStore accounts,
    IEmailQueue emailQueue,
    TimeProvider timeProvider,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    public async Task DispatchAsync(
        NotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Kind = request.Kind,
            Title = request.Title,
            TitleArabic = request.TitleArabic,
            Body = request.Body,
            BodyArabic = request.BodyArabic,
            Severity = request.Severity,
            RelatedEntityType = request.RelatedEntityType,
            RelatedEntityId = request.RelatedEntityId,
            CreatedAt = now,
        };

        await notifications.AddAsync(notification, cancellationToken);

        logger.LogInformation(
            "Notification {Kind} dispatched for {UserId} (id {Id})",
            request.Kind, request.UserId, notification.Id);

        if (request.SendEmail)
        {
            // P13 will set PreRenderedEmailHtml on every call site. P12
            // ships the dispatcher able to handle the pre-rendered path
            // already so the contract does not change later.
            var user = await accounts.FindByIdAsync(request.UserId, cancellationToken);
            if (user?.Email is null)
            {
                logger.LogWarning(
                    "Notification {Kind} for {UserId}: email skipped — no address on file.",
                    request.Kind, request.UserId);
                return;
            }
            // Subject = the English title; the user's culture decides
            // the body. A future culture-aware queue / per-user
            // language preference is a P13 follow-up.
            var body = request.PreRenderedEmailHtml
                ?? $"<p>{System.Net.WebUtility.HtmlEncode(request.Body)}</p>";
            emailQueue.Enqueue(new EmailMessage(user.Email, request.Title, body));
        }
    }
}
