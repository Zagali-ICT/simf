using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Notifications;
using SIMF.Domain.Notifications;

namespace SIMF.Application.Notifications;

/// <summary>
/// Persistence seam for the in-app notification surface.
/// Application services (<c>NotificationDispatcher</c>,
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

    /// <summary>Writes a whole batch of rows in ONE round-trip.
    ///
    /// <para>A fan-out to an audience — an "Everyone" announcement, a 400-seat
    /// session's absentees — used to reach this repository one row at a time, so
    /// 20,000 recipients meant 20,000 sequential INSERT + SaveChanges round-trips
    /// against the Identity DB while the broadcast worker sat blocked. The batch
    /// is all-or-nothing, which is why the callers keep their per-recipient
    /// isolation as a fallback rather than dropping it.</para></summary>
    Task AddRangeAsync(
        IReadOnlyCollection<Notification> notifications,
        CancellationToken cancellationToken = default);

    /// <summary>True when a notification of <paramref name="kind"/> for
    /// <paramref name="relatedEntityId"/> already exists for
    /// <paramref name="userId"/>. Backs the dispatcher's opt-in
    /// <see cref="NotificationRequest.DeduplicateByRelatedEntity"/> guard so the
    /// same (user, kind, entity) is never notified twice — e.g. one session-rating
    /// prompt per attendee whether it fires on hall departure or from the
    /// clock-end worker. A single-context query on the Identity DB — there is no
    /// cross-DB join, because the entity id is a bare Guid.</summary>
    Task<bool> ExistsForUserAsync(
        Guid userId,
        NotificationKind kind,
        Guid relatedEntityId,
        CancellationToken cancellationToken = default);

    /// <summary>The subset of <paramref name="userIds"/> that already hold a
    /// notification of <paramref name="kind"/> for
    /// <paramref name="relatedEntityId"/> — the batched form of
    /// <see cref="ExistsForUserAsync"/>.
    ///
    /// <para>The per-user form costs one AnyAsync per recipient, and the
    /// not-attended sweep re-runs over the same absentees every minute for the
    /// whole reminder window: 400 absentees came to roughly 8,000 existence
    /// queries per session. One IN-list query answers the same question for the
    /// whole batch.</para></summary>
    Task<IReadOnlyCollection<Guid>> ExistingUserIdsAsync(
        NotificationKind kind,
        Guid relatedEntityId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<NotificationDto> Items, int Total)> ListForUserAsync(
        Guid userId,
        int skip,
        int top,
        bool unreadOnly,
        // A8 — optional server-side kind narrow; null/empty = all kinds. Appended
        // (defaulted → no other caller breaks).
        IReadOnlyCollection<NotificationKind>? kinds = null,
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
        DateTime readAt,
        CancellationToken cancellationToken = default);

    Task MarkAllReadForUserAsync(
        Guid userId,
        DateTime readAt,
        CancellationToken cancellationToken = default);

    Task DeleteForUserAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);
}
