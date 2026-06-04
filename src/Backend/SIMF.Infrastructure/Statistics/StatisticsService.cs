// Tests: SIMF.Api.Tests/StatisticsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Statistics.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Statistics;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Statistics;

/// <summary>
/// Vertical S — computes the Control Panel overview dashboard. Each metric is
/// its own COUNT / AVG query so one expensive or empty aggregate never affects
/// the others, and every query is <c>AsNoTracking</c> (these are pure reads —
/// nothing is materialised into the change tracker).
///
/// <para>Attendee counts come from the Identity DB: an attendee is any
/// non-admin account (<see cref="UserType.Visitor"/>); "approved" filters on
/// <see cref="AccountState.Approved"/> and "pending" on
/// <see cref="AccountState.PendingApproval"/>. The event-module counts come
/// from the App DB and filter on the soft-delete flag where the entity carries
/// one, matching the public/admin list behaviour (CLAUDE.md §7).</para>
/// </summary>
internal sealed class StatisticsService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext) : IStatisticsService
{
    public async Task<StatisticsDashboard> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        // Identity DB — attendees are non-admin (Visitor) accounts.
        var totalAttendees = await identityDbContext.Users.AsNoTracking()
            .CountAsync(u => u.UserType == UserType.Visitor, cancellationToken);

        var approvedAttendees = await identityDbContext.Users.AsNoTracking()
            .CountAsync(
                u => u.UserType == UserType.Visitor
                    && u.AccountState == AccountState.Approved,
                cancellationToken);

        var pendingApprovals = await identityDbContext.Users.AsNoTracking()
            .CountAsync(
                u => u.UserType == UserType.Visitor
                    && u.AccountState == AccountState.PendingApproval,
                cancellationToken);

        // App DB — event-module counts (active rows only where soft-deleted).
        var sessions = await appDbContext.Sessions.AsNoTracking()
            .CountAsync(s => s.IsActive, cancellationToken);

        var speakers = await appDbContext.Speakers.AsNoTracking()
            .CountAsync(s => s.IsActive, cancellationToken);

        var booths = await appDbContext.Booths.AsNoTracking()
            .CountAsync(b => b.IsActive, cancellationToken);

        var sponsors = await appDbContext.Sponsors.AsNoTracking()
            .CountAsync(s => s.IsActive, cancellationToken);

        var newsArticles = await appDbContext.News.AsNoTracking()
            .CountAsync(n => n.IsActive, cancellationToken);

        var mediaItems = await appDbContext.MediaItems.AsNoTracking()
            .CountAsync(m => m.IsActive, cancellationToken);

        var commentsApproved = await appDbContext.SessionComments.AsNoTracking()
            .CountAsync(
                c => c.IsActive && c.Status == SessionCommentStatus.Approved,
                cancellationToken);

        var commentsPending = await appDbContext.SessionComments.AsNoTracking()
            .CountAsync(
                c => c.IsActive && c.Status == SessionCommentStatus.Pending,
                cancellationToken);

        var ratingsCount = await appDbContext.Ratings.AsNoTracking()
            .CountAsync(r => r.IsActive, cancellationToken);

        // Average over active ratings only. The nullable cast makes an empty
        // set return null (not throw on AverageAsync), folded to 0.
        var averageRating = await appDbContext.Ratings.AsNoTracking()
            .Where(r => r.IsActive)
            .Select(r => (double?)r.Stars)
            .AverageAsync(cancellationToken) ?? 0;

        return new StatisticsDashboard(
            totalAttendees,
            approvedAttendees,
            pendingApprovals,
            sessions,
            speakers,
            booths,
            sponsors,
            newsArticles,
            mediaItems,
            commentsApproved,
            commentsPending,
            ratingsCount,
            averageRating);
    }
}
