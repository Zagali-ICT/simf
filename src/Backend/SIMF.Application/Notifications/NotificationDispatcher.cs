// Tests: SIMF.Api.Tests/NotificationTests.cs (round-trips)
//        SIMF.Api.Tests/NotificationLifecycleTests.cs (trigger-by-trigger)
//        SIMF.Api.Tests/NotificationChannelTests.cs (the INotificationChannel seam)
using Microsoft.Extensions.Logging;

namespace SIMF.Application.Notifications;

/// <summary>
/// Dispatches one notification across every registered
/// <see cref="INotificationChannel"/>. The in-app row write always
/// lands first; an email failure never poisons the in-app delivery — they are
/// independent channels.
///
/// <para>This type lives in the Application layer, not
/// <c>SIMF.Infrastructure.Notifications</c>.</para>
///
/// <para>The two hard-coded deliveries (write the row,
/// then enqueue an email) moved out to <see cref="InAppNotificationChannel"/> and
/// <see cref="EmailNotificationChannel"/>. What is left here is the only policy
/// common to every channel: the dedup guard. A future SMS / WhatsApp
/// transport is one new <see cref="INotificationChannel"/> plus one DI line — this
/// class does not change again.</para>
/// </summary>
internal sealed class NotificationDispatcher(
    IEnumerable<INotificationChannel> channels,
    INotificationRepository notifications,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    public async Task DispatchAsync(
        NotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        // Opt-in one-per-(user, kind, entity) guard. When the caller asks
        // to deduplicate and a matching notification already exists, skip the whole
        // dispatch (every channel) so a session-rating prompt never double-fires
        // across the hall-departure hook (GAP-A) and the clock-end worker. It lives
        // here, not in a channel: it is a policy about the dispatch as a whole.
        if (request.DeduplicateByRelatedEntity
            && request.RelatedEntityId is { } relatedId
            && await notifications.ExistsForUserAsync(
                request.UserId, request.Kind, relatedId, cancellationToken))
        {
            logger.LogInformation(
                "Notification {Kind} for {UserId} skipped — already sent for entity {EntityId}.",
                request.Kind, request.UserId, relatedId);
            return;
        }

        // Ascending Order, so the in-app row (0) is written before any outbound
        // transport runs — the ordering the old inline code relied on.
        foreach (var channel in channels.OrderBy(channel => channel.Order))
        {
            if (!channel.ShouldHandle(request))
            {
                continue;
            }
            await channel.SendAsync(request, cancellationToken);
        }
    }
}
