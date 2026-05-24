// Tests: SIMF.Api.Tests/NotificationTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Notifications;
using SIMF.Common;
using SIMF.Contracts.Notifications;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Notifications;

/// <summary>Reads + mutates the actor's notifications (P12 — D-053).</summary>
internal sealed class NotificationService(
    SimfIdentityDbContext dbContext,
    TimeProvider timeProvider) : INotificationService
{
    public async Task<GridPage<NotificationDto>> ListMineAsync(
        Guid actorUserId, GridQuery query,
        CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = dbContext.Notifications
            .AsNoTracking()
            .Where(row => row.UserId == actorUserId);

        // Optional "unreadOnly=true" filter for the bell dropdown.
        if (query.Filters.TryGetValue("unreadOnly", out var unreadFilter)
            && bool.TryParse(unreadFilter, out var unreadOnly)
            && unreadOnly)
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

        return GridPage<NotificationDto>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public Task<int> UnreadCountMineAsync(
        Guid actorUserId, CancellationToken cancellationToken = default) =>
        dbContext.Notifications
            .AsNoTracking()
            .Where(row => row.UserId == actorUserId && row.ReadAt == null)
            .CountAsync(cancellationToken);

    public async Task MarkReadMineAsync(
        Guid actorUserId, Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.Notifications
            .SingleOrDefaultAsync(
                n => n.Id == notificationId && n.UserId == actorUserId,
                cancellationToken);
        if (row is null || row.ReadAt is not null) return;
        row.ReadAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadMineAsync(
        Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        await dbContext.Notifications
            .Where(row => row.UserId == actorUserId && row.ReadAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(row => row.ReadAt, now),
                cancellationToken);
    }

    public async Task DeleteMineAsync(
        Guid actorUserId, Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Notifications
            .Where(row => row.Id == notificationId && row.UserId == actorUserId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
