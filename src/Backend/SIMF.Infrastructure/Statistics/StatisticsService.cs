// Tests: SIMF.Api.Tests/StatisticsProgrammeTests.cs
// (the previously referenced StatisticsTests.cs does not exist — GetDashboardAsync
//  has no direct coverage; noted rather than silently left pointing at nothing.)
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Statistics.Abstractions;
using SIMF.Common;
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

        var ratingsCount = await appDbContext.RatingResponses.AsNoTracking()
            .CountAsync(r => r.IsActive, cancellationToken);

        // Average over the overall score of active responses (responses that
        // carry one). The nullable cast makes an empty set return null (not throw
        // on AverageAsync), folded to 0.
        var averageRating = await appDbContext.RatingResponses.AsNoTracking()
            .Where(r => r.IsActive && r.OverallStars != null)
            .Select(r => (double?)r.OverallStars!.Value)
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
            ratingsCount,
            averageRating);
    }

    public async Task<StatisticsProgramme> GetProgrammeAsync(
        CancellationToken cancellationToken = default)
    {
        // ---- Headline participant counts -----------------------------------
        var currentUsers = await identityDbContext.Users.AsNoTracking()
            .CountAsync(cancellationToken);

        // Role counts resolve through UserProfile -> UserProfileType. Both
        // tables live in the App DB, so this is a single-database join and
        // never a cross-context query (D-157). Which profile type counts as
        // staff / exhibitor is admin-curated data (ProfileType.MobileAppRole,
        // ProfileType.IsForVisitor) — never a hardcoded role name.
        var profiles = appDbContext.UserProfiles.AsNoTracking().Where(p => p.IsActive);

        // A profile with no type assigned counts as a visitor: IsForVisitor
        // itself defaults to true ("audience-side until an admin says
        // otherwise"), and ExhibitorVisitorService already reads an absent type
        // the same way. Requiring a non-null type here would quietly undercount
        // every account that has not been categorised yet.
        var visitors = await profiles.CountAsync(
            p => p.ProfileType == null || p.ProfileType.IsForVisitor, cancellationToken);

        var staff = await profiles.CountAsync(
            p => p.ProfileType != null
                && p.ProfileType.MobileAppRole == MobileAppRole.Staff,
            cancellationToken);

        var moderators = await profiles.CountAsync(
            p => p.ProfileType != null
                && p.ProfileType.MobileAppRole == MobileAppRole.Moderator,
            cancellationToken);

        var exhibitorAccounts = await profiles.CountAsync(
            p => p.ProfileType != null
                && p.ProfileType.MobileAppRole == MobileAppRole.Exhibitor,
            cancellationToken);

        // Exhibitors / sponsors are the CP-managed organisations, not accounts.
        var exhibitors = await appDbContext.Exhibitors.AsNoTracking()
            .CountAsync(e => e.IsActive, cancellationToken);

        var sponsors = await appDbContext.Sponsors.AsNoTracking()
            .CountAsync(s => s.IsActive, cancellationToken);

        var speakers = await appDbContext.Speakers.AsNoTracking()
            .CountAsync(s => s.IsActive, cancellationToken);

        var booths = await appDbContext.Booths.AsNoTracking()
            .CountAsync(b => b.IsActive, cancellationToken);

        var totalAttended = await appDbContext.HallAttendances.AsNoTracking()
            .Select(a => a.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        // ---- Per-forum-day figures -----------------------------------------
        // Sessions are matched to a day BY DATE, mirroring ProgrammeDay's
        // deliberate no-FK design — the app groups them the same way, so the
        // two surfaces cannot disagree about which session belongs to a day.
        var days = await appDbContext.ProgrammeDays.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.Date)
            .Select(d => new { d.Id, d.Date, d.Title, d.TitleArabic, d.DisplayOrder })
            .ToListAsync(cancellationToken);

        var dayStats = new List<ProgrammeDayStats>(days.Count);

        foreach (var day in days)
        {
            // The Saudi calendar day as an explicit half-open window
            // [start, end). Since D-813 instants ARE the Saudi wall clock, so
            // this is a straight range on the stored value with no zone shift;
            // FromSaudiWallClock is kept as the single named seam (it only
            // stamps the Kind now). A plain range predicate also stays an
            // index-friendly seek rather than relying on a translated
            // date-component expression.
            var startUtc = SaudiTime.FromSaudiWallClock(
                day.Date.ToDateTime(TimeOnly.MinValue));
            var endUtc = startUtc.AddDays(1);

            var registered = await identityDbContext.Users.AsNoTracking()
                .CountAsync(
                    u => u.UserType == UserType.Visitor
                        && u.CreatedAt >= startUtc && u.CreatedAt < endUtc,
                    cancellationToken);

            // Distinct people who were let in through a gate that day. A visitor
            // scanning twice counts once.
            var present = await appDbContext.GateScans.AsNoTracking()
                .Where(s => s.Outcome == ScanOutcome.Allowed
                    && s.Direction == ScanDirection.CheckIn
                    && s.UserProfileId != null
                    && s.ScannedAt >= startUtc && s.ScannedAt < endUtc)
                .Select(s => s.UserProfileId!.Value)
                .Distinct()
                .CountAsync(cancellationToken);

            var sessions = await appDbContext.Sessions.AsNoTracking()
                .CountAsync(
                    s => s.IsActive && s.Start >= startUtc && s.Start < endUtc,
                    cancellationToken);

            // Distinct people who arrived at any hall that day.
            var attended = await appDbContext.HallAttendances.AsNoTracking()
                .Where(a => a.Enter >= startUtc && a.Enter < endUtc)
                .Select(a => a.UserId)
                .Distinct()
                .CountAsync(cancellationToken);

            dayStats.Add(new ProgrammeDayStats(
                day.Id,
                day.Date,
                day.Title,
                day.TitleArabic,
                day.DisplayOrder,
                registered,
                present,
                sessions,
                attended));
        }

        return new StatisticsProgramme(
            currentUsers,
            visitors,
            staff,
            moderators,
            exhibitors,
            exhibitorAccounts,
            sponsors,
            speakers,
            booths,
            totalAttended,
            dayStats);
    }
}
