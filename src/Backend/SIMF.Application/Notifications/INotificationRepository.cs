using SIMF.Common;
using SIMF.Contracts.Notifications;
using SIMF.Domain.Notifications;

namespace SIMF.Application.Notifications;

/// <summary>
/// R4 — D-095: persistence seam for the in-app notification surface (P12 —
/// D-053). Application services (<c>NotificationDispatcher</c>,
/// <c>NotificationService</c>) talk to this contract; the Infrastructure
/// implementation owns the EF query shapes.
///
/// <para>Method names follow the sibling-repository idiom in this project
/// (<c>IRefreshTokenRepository.RevokeAllForUserAsync</c> etc.) — when an
/// operation is scoped to a single user the verb takes a <c>ForUser</c>
/// suffix rather than a redundant <c>ByOwner</c>.</para>
/// </summary>
public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<NotificationDto> Items, int Total)> ListForUserAsync(
        Guid userId,
        int skip,
        int top,
        bool unreadOnly,
        CancellationToken cancellationToken = default);

    Task<int> CountUnreadForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads + flips ReadAt for the single notification owned by
    /// <paramref name="userId"/>. Silent no-op when the notification
    /// is not found or already read.
    /// </summary>
    Task MarkReadForUserAsync(
        Guid userId,
        Guid notificationId,
        DateTimeOffset readAt,
        CancellationToken cancellationToken = default);

    Task MarkAllReadForUserAsync(
        Guid userId,
        DateTimeOffset readAt,
        CancellationToken cancellationToken = default);

    Task DeleteForUserAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);
}
