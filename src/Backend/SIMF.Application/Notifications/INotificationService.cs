using SIMF.Common;
using SIMF.Contracts.Notifications;

namespace SIMF.Application.Notifications;

/// <summary>
/// Read + mutate the actor's notifications (P12 — D-053). Every call
/// is actor-scoped — a user only ever sees their own rows.
/// </summary>
public interface INotificationService
{
    /// <summary>One page of the actor's notifications, newest first.</summary>
    Task<GridPage<NotificationDto>> ListMineAsync(
        Guid actorUserId,
        GridQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>How many unread notifications the actor has.</summary>
    Task<int> UnreadCountMineAsync(
        Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Flags one notification as read. Idempotent.</summary>
    Task MarkReadMineAsync(
        Guid actorUserId, Guid notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>Flags every unread notification as read.</summary>
    Task MarkAllReadMineAsync(
        Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Removes a notification. Idempotent.</summary>
    Task DeleteMineAsync(
        Guid actorUserId, Guid notificationId,
        CancellationToken cancellationToken = default);
}
