// Tests: SIMF.Api.Tests/NotificationTests.cs
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Notifications;

namespace SIMF.Application.Notifications;

/// <summary>
/// Reads + mutates the actor's notifications. Moved here from
/// <c>SIMF.Infrastructure.Notifications</c>; persistence is
/// delegated to <see cref="INotificationRepository"/>.
/// </summary>
internal sealed class NotificationService(
    INotificationRepository notifications,
    TimeProvider timeProvider) : INotificationService
{
    public async Task<GridPage<NotificationDto>> ListMineAsync(
        Guid actorUserId, GridQuery query,
        CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(25, 200);
        var unreadOnly =
            query.Filters.TryGetValue("unreadOnly", out var unreadFilter)
            && bool.TryParse(unreadFilter, out var parsed)
            && parsed;

        // Optional server-side kinds filter: comma-separated NotificationKind
        // names in Filters["kinds"] (mirrors the unreadOnly key). Unknown tokens are
        // dropped (a newer app kind never 400s an older server); if nothing parses,
        // the filter is treated as absent (all kinds), matching the tolerant
        // unreadOnly parse. Enum.TryParse also accepts a raw numeric string for an
        // UNDEFINED value (e.g. "99999"), so Enum.IsDefined gates it out — otherwise
        // such a token would survive as a phantom kind that matches no persisted row
        // (empty result) instead of the documented all-kinds fallback.
        List<NotificationKind>? kinds = null;
        if (query.Filters.TryGetValue("kinds", out var kindsFilter)
            && !string.IsNullOrWhiteSpace(kindsFilter))
        {
            kinds = kindsFilter
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(token =>
                    Enum.TryParse<NotificationKind>(token, ignoreCase: true, out var k)
                    && Enum.IsDefined(typeof(NotificationKind), k)
                        ? k
                        : (NotificationKind?)null)
                .Where(k => k.HasValue)
                .Select(k => k!.Value)
                .Distinct()
                .ToList();
            if (kinds.Count == 0) { kinds = null; }
        }

        var (items, total) = await notifications.ListForUserAsync(
            actorUserId, skip, top, unreadOnly, kinds, cancellationToken);

        return GridPage<NotificationDto>.Of(items, total,
            skip, top);
    }

    public Task<int> UnreadCountMineAsync(
        Guid actorUserId, CancellationToken cancellationToken = default) =>
        notifications.CountUnreadForUserAsync(actorUserId, cancellationToken);

    public Task MarkReadMineAsync(
        Guid actorUserId, Guid notificationId,
        CancellationToken cancellationToken = default) =>
        notifications.MarkReadForUserAsync(
            actorUserId, notificationId, timeProvider.SimfNow(), cancellationToken);

    public Task MarkAllReadMineAsync(
        Guid actorUserId, CancellationToken cancellationToken = default) =>
        notifications.MarkAllReadForUserAsync(
            actorUserId, timeProvider.SimfNow(), cancellationToken);

    public Task DeleteMineAsync(
        Guid actorUserId, Guid notificationId,
        CancellationToken cancellationToken = default) =>
        notifications.DeleteForUserAsync(
            actorUserId, notificationId, cancellationToken);
}
