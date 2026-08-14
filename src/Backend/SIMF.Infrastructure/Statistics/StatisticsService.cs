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
/// <para>Attendee counts come from <c>UserProfile</c> in the App DB, not from
/// Identity accounts: an attendee may have no account at all, and the profile is
/// the row that exists for every one of them. "Approved" and "pending" filter on
/// the profile's own <c>AdmissionState</c>, which is what decides entry. The
/// event-module counts also come from the App DB and filter on the soft-delete
/// flag where the entity carries one, matching the public/admin list behaviour
/// (CLAUDE.md §7).</para>
/// </summary>
// Every figure now comes from the App DB, so the Identity context is no longer
// injected: nothing here reads an account.
internal sealed class StatisticsService(
    SimfAppDbContext appDbContext) : IStatisticsService
{
    public async Task<StatisticsDashboard> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        // Attendees are counted from PROFILES, and their approval from the
        // profile's own admission state. Counting Identity users would miss
        // every attendee who has no account — a walk-in registration or a
        // pre-generated badge — which is a large share of a real event, and
        // would read approval from a row that no longer decides it.
        //
        // An absent profile type reads as audience, matching how the programme
        // breakdown below and ExhibitorVisitorService already treat it:
        // IsForVisitor itself defaults to true, so requiring a non-null type
        // would quietly undercount everyone not yet categorised.
        var attendees = appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.IsActive
                && (p.ProfileType == null || p.ProfileType.IsForVisitor));

        var totalAttendees = await attendees.CountAsync(cancellationToken);

        var approvedAttendees = await attendees.CountAsync(
            p => p.AdmissionState == AccountState.Approved, cancellationToken);

        var pendingApprovals = await attendees.CountAsync(
            p => p.AdmissionState == AccountState.PendingApproval, cancellationToken);

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
        // Role counts resolve through UserProfile -> UserProfileType. Both
        // tables live in the App DB, so this is a single-database join and
        // never a cross-context query. Which profile type counts as
        // staff / exhibitor is admin-curated data (ProfileType.MobileAppRole,
        // ProfileType.IsForVisitor) — never a hardcoded role name.
        var profiles = appDbContext.UserProfiles.AsNoTracking().Where(p => p.IsActive);

        // ---- Headline participant count ------------------------------------
        // Counted from PROFILES, like every breakdown below it. It used to count
        // Identity users, which made the headline and its own breakdown answer
        // two different questions in one response: an attendee registered at a
        // desk has a profile and no account, so the parts could exceed the whole
        // and the page could report more people present than registered.
        var currentUsers = await profiles.CountAsync(cancellationToken);

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
            .Select(a => a.UserProfileId)
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
            // [start, end). Stored instants ARE the Saudi wall clock, so
            // this is a straight range on the stored value with no zone shift;
            // FromSaudiWallClock is kept as the single named seam (it only
            // stamps the Kind now). A plain range predicate also stays an
            // index-friendly seek rather than relying on a translated
            // date-component expression.
            var startUtc = SaudiTime.FromSaudiWallClock(
                day.Date.ToDateTime(TimeOnly.MinValue));
            var endUtc = startUtc.AddDays(1);

            // Counted from profiles, so it is comparable with `present` just
            // below, which comes from gate scans and therefore includes people
            // who never held an account. Counting Identity users here made a
            // walk-in day report more attendees present than registered.
            var registered = await appDbContext.UserProfiles.AsNoTracking()
                .CountAsync(
                    p => p.IsActive
                        && (p.ProfileType == null || p.ProfileType.IsForVisitor)
                        && p.CreatedAt >= startUtc && p.CreatedAt < endUtc,
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
                .Select(a => a.UserProfileId)
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
