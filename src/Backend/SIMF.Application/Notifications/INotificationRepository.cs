using SIMF.Common;
using SIMF.Contracts.Notifications;
using SIMF.Domain.Notifications;

namespace SIMF.Application.Notifications;

/// <summary>
/// R4 — D-095: persistence seam for the in-app notification surface (P12 —
/// D-053). Application services (<c>NotificationDispatcher</c>,
/// <c>NotificationService</c>) talk to this contract; the Infrastructure
/// implementation owns the EF query shapes.
/// </summary>
public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<NotificationDto> Items, int Total)> ListByOwnerAsync(
        Guid ownerUserId,
        int skip,
        int top,
        bool unreadOnly,
        CancellationToken cancellationToken = default);

    Task<int> CountUnreadByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads + flips ReadAt for the single notification owned by
    /// <paramref name="ownerUserId"/>. Silent no-op when the notification
    /// is not found or already read.
    /// </summary>
    Task MarkReadByOwnerAsync(
        Guid ownerUserId,
        Guid notificationId,
        DateTimeOffset readAt,
        CancellationToken cancellationToken = default);

    Task MarkAllReadByOwnerAsync(
        Guid ownerUserId,
        DateTimeOffset readAt,
        CancellationToken cancellationToken = default);

    Task DeleteByOwnerAsync(
        Guid ownerUserId,
        Guid notificationId,
        CancellationToken cancellationToken = default);
}
