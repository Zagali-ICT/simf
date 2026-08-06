using Microsoft.EntityFrameworkCore;
using SIMF.Application.Notifications;
using SIMF.Common.Enums;
using SIMF.Contracts.Notifications;
using SIMF.Domain.Notifications;

namespace SIMF.Infrastructure.Persistence.Repositories;

/// <summary>EF-backed <see cref="INotificationRepository"/>.</summary>
internal sealed class NotificationRepository(SimfIdentityDbContext dbContext)
    : INotificationRepository
{
    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ExistsForUserAsync(
        Guid userId, NotificationKind kind, Guid relatedEntityId,
        CancellationToken cancellationToken = default) =>
        dbContext.Notifications
            .AsNoTracking()
            .AnyAsync(
                row => row.UserId == userId
                    && row.Kind == kind
                    && row.RelatedEntityId == relatedEntityId,
                cancellationToken);

    public async Task<(IReadOnlyList<NotificationDto> Items, int Total)> ListForUserAsync(
        Guid userId, int skip, int top, bool unreadOnly,
        IReadOnlyCollection<NotificationKind>? kinds = null,
        CancellationToken cancellationToken = default)
    {
        var rows = dbContext.Notifications
            .AsNoTracking()
            .Where(row => row.UserId == userId);

        if (unreadOnly)
        {
            rows = rows.Where(row => row.ReadAt == null);
        }

        // A8 — optional kind narrow, applied BEFORE the count so Total reflects it.
        // Kind is string-mapped (D-108), so this translates to WHERE Kind IN (...).
        if (kinds is { Count: > 0 })
        {
            rows = rows.Where(row => kinds.Contains(row.Kind));
        }

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .OrderByDescending(row => row.CreatedAt)
            .Skip(skip)
            .Take(top)
            .Select(row => new NotificationDto(
                row.Id,
                row.Kind.ToString(),
                row.Title,
                row.TitleArabic,
                row.Body,
                row.BodyArabic,
                row.Severity.ToString(),
                row.ReadAt,
                row.ReadAt != null,
                row.CreatedAt,
                row.RelatedEntityType,
                row.RelatedEntityId,
                row.ClickUrl,
                row.GroupCode))
            .ToListAsync(cancellationToken);

        return (page, total);
    }

    public Task<int> CountUnreadForUserAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Notifications
            .AsNoTracking()
            .Where(row => row.UserId == userId && row.ReadAt == null)
            .CountAsync(cancellationToken);

    public async Task MarkReadForUserAsync(
        Guid userId, Guid notificationId, DateTime readAt,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.Notifications
            .SingleOrDefaultAsync(
                n => n.Id == notificationId && n.UserId == userId,
                cancellationToken);
        if (row is null || row.ReadAt is not null) { return; }
        row.ReadAt = readAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task MarkAllReadForUserAsync(
        Guid userId, DateTime readAt,
        CancellationToken cancellationToken = default) =>
        dbContext.Notifications
            .Where(row => row.UserId == userId && row.ReadAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(row => row.ReadAt, readAt),
                cancellationToken);

    public Task DeleteForUserAsync(
        Guid userId, Guid notificationId,
        CancellationToken cancellationToken = default) =>
        dbContext.Notifications
            .Where(row => row.Id == notificationId && row.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
}
