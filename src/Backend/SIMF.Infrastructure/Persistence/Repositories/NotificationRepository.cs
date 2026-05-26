using Microsoft.EntityFrameworkCore;
using SIMF.Application.Notifications;
using SIMF.Contracts.Notifications;
using SIMF.Domain.Notifications;

namespace SIMF.Infrastructure.Persistence.Repositories;

/// <summary>R4 — D-095: EF-backed <see cref="INotificationRepository"/>.</summary>
internal sealed class NotificationRepository(SimfIdentityDbContext dbContext)
    : INotificationRepository
{
    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<NotificationDto> Items, int Total)> ListByOwnerAsync(
        Guid ownerUserId, int skip, int top, bool unreadOnly,
        CancellationToken cancellationToken = default)
    {
        var rows = dbContext.Notifications
            .AsNoTracking()
            .Where(row => row.UserId == ownerUserId);

        if (unreadOnly)
        {
            rows = rows.Where(row => row.ReadAt == null);
        }

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .OrderByDescending(row => row.CreatedAt)
            .Skip(skip)
            .Take(top)
            .Select(row => new NotificationDto(
                row.Id,
                row.Kind,
                row.Title,
                row.TitleArabic,
                row.Body,
                row.BodyArabic,
                row.Severity.ToString(),
                row.ReadAt,
                row.CreatedAt,
                row.RelatedEntityType,
                row.RelatedEntityId))
            .ToListAsync(cancellationToken);

        return (page, total);
    }

    public Task<int> CountUnreadByOwnerAsync(
        Guid ownerUserId, CancellationToken cancellationToken = default) =>
        dbContext.Notifications
            .AsNoTracking()
            .Where(row => row.UserId == ownerUserId && row.ReadAt == null)
            .CountAsync(cancellationToken);

    public async Task MarkReadByOwnerAsync(
        Guid ownerUserId, Guid notificationId, DateTimeOffset readAt,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.Notifications
            .SingleOrDefaultAsync(
                n => n.Id == notificationId && n.UserId == ownerUserId,
                cancellationToken);
        if (row is null || row.ReadAt is not null) { return; }
        row.ReadAt = readAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task MarkAllReadByOwnerAsync(
        Guid ownerUserId, DateTimeOffset readAt,
        CancellationToken cancellationToken = default) =>
        dbContext.Notifications
            .Where(row => row.UserId == ownerUserId && row.ReadAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(row => row.ReadAt, readAt),
                cancellationToken);

    public Task DeleteByOwnerAsync(
        Guid ownerUserId, Guid notificationId,
        CancellationToken cancellationToken = default) =>
        dbContext.Notifications
            .Where(row => row.Id == notificationId && row.UserId == ownerUserId)
            .ExecuteDeleteAsync(cancellationToken);
}
