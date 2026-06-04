// Tests: SIMF.Api.Tests/NotificationTests.cs
using SIMF.Common;
using SIMF.Contracts.Notifications;

namespace SIMF.Application.Notifications;

/// <summary>
/// Reads + mutates the actor's notifications (P12 — D-053). R4 — D-095:
/// moved from <c>SIMF.Infrastructure.Notifications</c>; persistence is
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
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);
        var unreadOnly =
            query.Filters.TryGetValue("unreadOnly", out var unreadFilter)
            && bool.TryParse(unreadFilter, out var parsed)
            && parsed;

        var (items, total) = await notifications.ListForUserAsync(
            actorUserId, skip, top, unreadOnly, cancellationToken);

        return GridPage<NotificationDto>.Of(items, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public Task<int> UnreadCountMineAsync(
        Guid actorUserId, CancellationToken cancellationToken = default) =>
        notifications.CountUnreadForUserAsync(actorUserId, cancellationToken);

    public Task MarkReadMineAsync(
        Guid actorUserId, Guid notificationId,
        CancellationToken cancellationToken = default) =>
        notifications.MarkReadForUserAsync(
            actorUserId, notificationId, timeProvider.GetUtcNow(), cancellationToken);

    public Task MarkAllReadMineAsync(
        Guid actorUserId, CancellationToken cancellationToken = default) =>
        notifications.MarkAllReadForUserAsync(
            actorUserId, timeProvider.GetUtcNow(), cancellationToken);

    public Task DeleteMineAsync(
        Guid actorUserId, Guid notificationId,
        CancellationToken cancellationToken = default) =>
        notifications.DeleteForUserAsync(
            actorUserId, notificationId, cancellationToken);
}
