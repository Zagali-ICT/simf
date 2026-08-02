// Tests: SIMF.Api.Tests/MyAreaDashboardTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess;
using SIMF.Application.MyArea;
using SIMF.Common.Enums;
using SIMF.Contracts.Account;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

namespace SIMF.Infrastructure.MyArea;

/// <summary>
/// Builds the My-Area dashboard (App Screen 14) read model. Three read-only
/// aggregates over the App DB — held seat bookings (Page_014 L-2), accepted
/// speaker meetings + confirmed business meetings (L-3), merged into today's
/// schedule (L-4) — plus the user's profile / tier and the account avatar. The
/// avatar comes from the Identity side via <see cref="IAccountService"/>, a
/// second read on the other context (D-157 — no cross-DB join). D-249.
/// </summary>
internal sealed class MyAreaService(
    SimfAppDbContext appDbContext,
    IAccountService accountService,
    TimeProvider timeProvider) : IMyAreaService
{
    private const string KindSession = "Session";
    private const string KindMeeting = "Meeting";

    /// <summary>The event runs in Riyadh — Arabia Standard Time (+03:00, no DST)
    /// — so "today" on the dashboard is the AST calendar day, not the calendar day elsewhere
    /// (otherwise an evening session would slip to the next day's card).</summary>
    private static readonly TimeSpan EventTimeZoneOffset = TimeSpan.FromHours(3);

    public async Task<MyAreaDashboard> GetDashboardAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var identity = await LoadIdentityAsync(userId, cancellationToken);
        var items = await LoadScheduleAsync(userId, cancellationToken);

        var counters = new MyAreaCounters(
            items.Count(i => i.Kind == KindSession),
            items.Count(i => i.Kind == KindMeeting));

        var (todayStart, todayEnd) = TodayWindow();
        var today = items
            .Where(i => i.Start >= todayStart && i.Start < todayEnd)
            .OrderBy(i => i.Start)
            .ToList();

        return new MyAreaDashboard(identity, counters, today);
    }

    public async Task<IReadOnlyList<MyAreaCalendarEvent>> GetCalendarEventsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var items = await LoadScheduleAsync(userId, cancellationToken);
        return items
            .OrderBy(i => i.Start)
            .Select(i => new MyAreaCalendarEvent(
                i.MeetingId ?? i.SessionId ?? Guid.Empty,
                i.Start,
                i.End,
                // Sessions are titled; meetings carry their subject (fall back to
                // the parent session title for a speaker meeting with no subject).
                i.Kind == KindSession || string.IsNullOrWhiteSpace(i.Subject)
                    ? i.TitleEn
                    : i.Subject!,
                i.HallNameEn))
            .ToList();
    }

    public async Task<MyAreaContactCard> GetContactCardAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var card = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new MyAreaContactCard(
                p.Name,
                p.NameArabic,
                p.JobTitle,
                p.JobTitleArabic,
                p.Organisation != null ? (p.Organisation.Name ?? p.Organisation.NameArabic) : null,
                p.QrId,
                p.SaudiMobile,
                p.InternationalMobile))
            .FirstOrDefaultAsync(cancellationToken);

        return card ?? new MyAreaContactCard(string.Empty, string.Empty, null, null, null, null);
    }

    public async Task<MyAreaSessions> GetMySessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // The user's booked / joined sessions — the same active seat-bookings the
        // dashboard counts (D-485 kinds, not released, active session). Project the
        // card fields + the primary speaker (DisplayOrder 0) to an anonymous type;
        // distinct by session, since a user may hold more than one active row.
        var rows = await appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.ReservedForUserId == userId
                && (r.Kind == SeatReservationKind.UserBooking
                    || r.Kind == SeatReservationKind.RandomAssignment
                    || r.Kind == SeatReservationKind.OpenSeating)
                && r.ReleasedAt == null
                && r.Session!.IsActive)
            .Select(r => new
            {
                r.SessionId,
                r.Session!.Title,
                r.Session.TitleArabic,
                r.Session.Start,
                r.Session.End,
                r.Session.Status,
                HallEn = r.Session.Hall!.Name,
                HallAr = r.Session.Hall.NameArabic,
                CategoryEn = r.Session.Category != null ? r.Session.Category.Name : null,
                CategoryAr = r.Session.Category != null ? r.Session.Category.NameArabic : null,
                Speaker = r.Session.Speakers
                    .OrderBy(ss => ss.DisplayOrder)
                    .Select(ss => new
                    {
                        ss.Speaker!.Name,
                        ss.Speaker.NameArabic,
                        ss.Speaker.Rank,
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        // The sessions the user actually arrived at (any HallAttendance row) and
        // the ones they hearted — two cheap id sets resolved on read.
        var attended = (await appDbContext.HallAttendances.AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => a.SessionId)
            .Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();

        var favourites = (await appDbContext.SessionFavourites.AsNoTracking()
            .Where(f => f.UserId == userId)
            .Select(f => f.SessionId)
            .ToListAsync(cancellationToken)).ToHashSet();

        var items = rows
            .GroupBy(r => r.SessionId)
            .Select(g => g.First())
            .OrderBy(r => r.Start)
            .Select(r => new MyAreaSessionItem(
                r.SessionId,
                r.Title,
                r.TitleArabic,
                r.Start,
                r.End,
                r.HallEn,
                r.HallAr,
                r.CategoryEn,
                r.CategoryAr,
                r.Speaker?.Name,
                r.Speaker?.NameArabic,
                r.Speaker?.Rank,
                r.Status,
                attended.Contains(r.SessionId),
                favourites.Contains(r.SessionId)))
            .ToList();

        return new MyAreaSessions(items);
    }

    private async Task<MyAreaIdentity> LoadIdentityAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await appDbContext.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new
            {
                p.NameArabic,
                p.Name,
                p.QrId,
                TierEn = p.ProfileType != null ? p.ProfileType.Name : null,
                TierAr = p.ProfileType != null ? p.ProfileType.NameArabic : null,
                Color = p.ProfileType != null ? p.ProfileType.PageColor : null,
                // Audience types are visitors; partner/exhibitor ("Other")
                // types are not (UserProfileType.IsForVisitor, D-186). No
                // ProfileType → treated as a visitor (D-426).
                IsVisitor = p.ProfileType == null || p.ProfileType.IsForVisitor,
            })
            .FirstOrDefaultAsync(cancellationToken);

        // The avatar lives on the account (Identity DB); resolve it on read.
        var account = await accountService.GetProfileAsync(userId, cancellationToken);

        return new MyAreaIdentity(
            profile?.NameArabic ?? string.Empty,
            profile?.Name ?? string.Empty,
            profile?.QrId,
            account.AvatarUrl,
            profile?.TierEn,
            profile?.TierAr,
            profile?.Color,
            profile?.IsVisitor ?? true);
    }

    /// <summary>
    /// Loads every schedule item for the user: held seat bookings (active
    /// sessions), accepted speaker meetings (active sessions), and confirmed
    /// business meetings. Each query projects to an anonymous type first, then
    /// maps in memory — so the enum-to-string and the union stay provider-safe.
    /// </summary>
    private async Task<List<MyAreaScheduleItem>> LoadScheduleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var sessions = await appDbContext.SeatReservations.AsNoTracking()
            .Where(r => r.ReservedForUserId == userId
                // D-485 — include OpenSeating joins (general admission) alongside
                // the seat-specific kinds so a joined session shows in the user's
                // schedule + booked-sessions count. AdminReservedRow has a null
                // ReservedForUserId, so it is already excluded.
                && (r.Kind == SeatReservationKind.UserBooking
                    || r.Kind == SeatReservationKind.RandomAssignment
                    || r.Kind == SeatReservationKind.OpenSeating)
                && r.ReleasedAt == null
                && r.Session!.IsActive)
            .Select(r => new
            {
                r.Session!.Start,
                r.Session.End,
                r.Session.Title,
                r.Session.TitleArabic,
                HallEn = r.Session.Hall!.Name,
                HallAr = r.Session.Hall.NameArabic,
                r.Status,
                r.SessionId,
            })
            .ToListAsync(cancellationToken);

        var businessMeetings = await appDbContext.BusinessMeetingParticipants.AsNoTracking()
            .Where(p => p.Kind == MeetingPartyKind.Visitor
                && p.VisitorUserId == userId
                && p.BusinessMeeting!.Status == BusinessMeetingStatus.Confirmed)
            .Select(p => new
            {
                p.BusinessMeeting!.Start,
                p.BusinessMeeting.End,
                HallEn = p.BusinessMeeting.MeetingTable!.Hall!.Name,
                HallAr = p.BusinessMeeting.MeetingTable.Hall.NameArabic,
                p.BusinessMeeting.Notes,
                p.BusinessMeetingId,
            })
            .ToListAsync(cancellationToken);

        var items = new List<MyAreaScheduleItem>(
            sessions.Count + businessMeetings.Count);

        items.AddRange(sessions.Select(s => new MyAreaScheduleItem(
            KindSession, s.Start, s.End, s.Title, s.TitleArabic,
            s.HallEn, s.HallAr, null, s.Status.ToString(), s.SessionId, null)));

        items.AddRange(businessMeetings.Select(b => new MyAreaScheduleItem(
            KindMeeting, b.Start, b.End, string.Empty, string.Empty,
            b.HallEn, b.HallAr, b.Notes, nameof(BusinessMeetingStatus.Confirmed),
            null, b.BusinessMeetingId)));

        return items;
    }

    /// <summary>Today's window in the event timezone (AST, +03:00), as a
    /// half-open Saudi-local interval. Deterministic via the injected
    /// <see cref="TimeProvider"/>.</summary>
    private (DateTime Start, DateTime End) TodayWindow()
    {
        var nowLocal = timeProvider.SimfNow();
        var startLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0);
        return (startLocal, startLocal.AddDays(1));
    }
}
