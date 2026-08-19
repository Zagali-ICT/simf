// Tests: SIMF.Api.Tests/NotificationTests.cs (round-trips)
//        SIMF.Api.Tests/NotificationLifecycleTests.cs (trigger-by-trigger)
//        SIMF.Api.Tests/NotificationChannelTests.cs (the INotificationChannel seam)
using Microsoft.Extensions.Logging;
using SIMF.Common.Enums;

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

    /// <summary>The batched dispatch. Same policy, same channel order, same rows —
    /// but the dedup guard is answered once per (kind, entity) rather than once per
    /// recipient, and each channel is handed the whole batch so the one whose cost
    /// is per round-trip can collapse it.</summary>
    public async Task DispatchManyAsync(
        IReadOnlyList<NotificationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var pending = await FilterAlreadySentAsync(requests, cancellationToken);
        if (pending.Count == 0)
        {
            return;
        }

        foreach (var channel in channels.OrderBy(channel => channel.Order))
        {
            var handled = pending.Where(channel.ShouldHandle).ToList();
            if (handled.Count == 0)
            {
                continue;
            }
            await channel.SendManyAsync(handled, cancellationToken);
        }
    }

    /// <summary>Drops the requests the opt-in one-per-(user, kind, entity) guard
    /// already covers. One IN-list query per distinct (kind, entity) — a sweep over
    /// 400 absentees used to ask that question 400 times, every minute, for the
    /// whole reminder window.</summary>
    private async Task<List<NotificationRequest>> FilterAlreadySentAsync(
        IReadOnlyList<NotificationRequest> requests, CancellationToken cancellationToken)
    {
        var guarded = requests
            .Where(request => request.DeduplicateByRelatedEntity
                && request.RelatedEntityId is not null)
            .ToList();
        if (guarded.Count == 0)
        {
            return [.. requests];
        }

        var alreadySent = new HashSet<(NotificationKind Kind, Guid EntityId, Guid UserId)>();
        foreach (var group in guarded.GroupBy(
            request => (request.Kind, EntityId: request.RelatedEntityId!.Value)))
        {
            var userIds = group.Select(request => request.UserId).Distinct().ToList();
            var existing = await notifications.ExistingUserIdsAsync(
                group.Key.Kind, group.Key.EntityId, userIds, cancellationToken);
            foreach (var userId in existing)
            {
                alreadySent.Add((group.Key.Kind, group.Key.EntityId, userId));
            }
        }

        var pending = new List<NotificationRequest>(requests.Count);
        foreach (var request in requests)
        {
            if (request.DeduplicateByRelatedEntity
                && request.RelatedEntityId is { } relatedId
                && alreadySent.Contains((request.Kind, relatedId, request.UserId)))
            {
                logger.LogInformation(
                    "Notification {Kind} for {UserId} skipped — already sent for entity {EntityId}.",
                    request.Kind, request.UserId, relatedId);
                continue;
            }
            pending.Add(request);
        }
        return pending;
    }
}
