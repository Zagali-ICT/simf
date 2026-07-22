using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Common.Enums;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Operations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Tests for the end-of-session rating-prompt scan. Exercises the internal
/// <see cref="SessionRatingPromptWorker.RunRatingPromptScanAsync"/> directly
/// (InternalsVisibleTo) so the once-only dedup and the back-fill window are
/// covered without driving the BackgroundService loop.
/// </summary>
public sealed class SessionRatingPromptWorkerTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public SessionRatingPromptWorkerTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Scan_prompts_attendees_of_a_just_ended_session_exactly_once()
    {
        var now = DateTimeOffset.UtcNow;
        var visitorId = await SeedVisitorAsync();
        // Ended five minutes ago — inside the back-fill window.
        var sessionId = await SeedEndedSessionWithAttendanceAsync(now.AddMinutes(-5), visitorId);

        var firstPass = await RunScanAsync(now);
        Assert.True(firstPass >= 1);

        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();

            var session = await appDb.Sessions.SingleAsync(s => s.Id == sessionId);
            Assert.NotNull(session.RatingPromptSentUtc);

            var count = await idDb.Notifications.CountAsync(n =>
                n.Kind == NotificationKind.SessionRatingRequest
                && n.RelatedEntityId == sessionId
                && n.UserId == visitorId);
            Assert.Equal(1, count);
        }

        // Second pass: the session is already stamped — no resend.
        await RunScanAsync(now);

        using (var scope = _factory.Services.CreateScope())
        {
            var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var count = await idDb.Notifications.CountAsync(n =>
                n.Kind == NotificationKind.SessionRatingRequest
                && n.RelatedEntityId == sessionId
                && n.UserId == visitorId);
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task Scan_does_not_resend_when_the_attendee_was_already_prompted_on_departure()
    {
        // D-713 (GAP-A) — the attendee left the hall (prompted once already);
        // the clock-end worker must not send a second rating prompt, though it
        // still stamps the session so it stops scanning it.
        var now = DateTimeOffset.UtcNow;
        var visitorId = await SeedVisitorAsync();
        var sessionId = await SeedEndedSessionWithAttendanceAsync(now.AddMinutes(-5), visitorId);
        await SeedExistingRatingPromptAsync(sessionId, visitorId);

        await RunScanAsync(now);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();

        var session = await appDb.Sessions.SingleAsync(s => s.Id == sessionId);
        Assert.NotNull(session.RatingPromptSentUtc);

        var count = await idDb.Notifications.CountAsync(n =>
            n.Kind == NotificationKind.SessionRatingRequest
            && n.RelatedEntityId == sessionId
            && n.UserId == visitorId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Scan_does_not_prompt_a_booked_but_absent_user()
    {
        // The reported leak: a seat BOOKING is not attendance. A user who reserved
        // a seat but never checked in to the hall must NOT get the rating prompt
        // (matching the submit gate). The session is still stamped so the worker
        // stops re-scanning it.
        var now = DateTimeOffset.UtcNow;
        var visitorId = await SeedVisitorAsync();
        var sessionId = await SeedEndedSessionWithBookingOnlyAsync(now.AddMinutes(-5), visitorId);

        var prompted = await RunScanAsync(now);
        Assert.True(prompted >= 1); // the session was in-window and scanned

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();

        // Stamped (so it stops scanning) but no prompt sent to the absent booker.
        var session = await appDb.Sessions.SingleAsync(s => s.Id == sessionId);
        Assert.NotNull(session.RatingPromptSentUtc);

        var count = await idDb.Notifications.CountAsync(n =>
            n.Kind == NotificationKind.SessionRatingRequest
            && n.RelatedEntityId == sessionId
            && n.UserId == visitorId);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Scan_ignores_a_session_that_ended_before_the_backfill_window()
    {
        var now = DateTimeOffset.UtcNow;
        var visitorId = await SeedVisitorAsync();
        // Ended eight hours ago — beyond the 6-hour back-fill window.
        var sessionId = await SeedEndedSessionWithAttendanceAsync(now.AddHours(-8), visitorId);

        await RunScanAsync(now);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var session = await appDb.Sessions.SingleAsync(s => s.Id == sessionId);
        Assert.Null(session.RatingPromptSentUtc);
    }

    [Fact]
    public async Task Scan_ignores_a_session_that_has_not_ended_yet()
    {
        var now = DateTimeOffset.UtcNow;
        var visitorId = await SeedVisitorAsync();
        // Ends in an hour — not yet over.
        var sessionId = await SeedEndedSessionWithAttendanceAsync(now.AddHours(1), visitorId);

        await RunScanAsync(now);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var session = await appDb.Sessions.SingleAsync(s => s.Id == sessionId);
        Assert.Null(session.RatingPromptSentUtc);
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task<int> RunScanAsync(DateTimeOffset now)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
        return await SessionRatingPromptWorker.RunRatingPromptScanAsync(
            db, dispatcher, now, SessionRatingPromptWorker.BackfillWindow,
            NullLogger.Instance, CancellationToken.None);
    }

    private async Task SeedExistingRatingPromptAsync(Guid sessionId, Guid visitorId)
    {
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
        await dispatcher.DispatchAsync(new NotificationRequest
        {
            UserId = visitorId,
            Kind = NotificationKind.SessionRatingRequest,
            Title = "Rate this session",
            TitleArabic = "قيّم هذه الجلسة",
            Body = "Departure prompt.",
            BodyArabic = "طلب عند المغادرة.",
            RelatedEntityType = "Session",
            RelatedEntityId = sessionId,
            SendEmail = false,
            DeduplicateByRelatedEntity = true,
        }, CancellationToken.None);
    }

    private async Task<Guid> SeedVisitorAsync()
    {
        var email = $"rate-prompt-visitor-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Rate Prompt Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    // Seeds a just-/long-ended session plus a HallAttendance (hall check-in) for
    // the visitor — the attendance the rating prompt is now gated on.
    private async Task<Guid> SeedEndedSessionWithAttendanceAsync(DateTimeOffset endUtc, Guid visitorId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var (hall, session) = BuildEndedSession(endUtc);
        db.Halls.Add(hall);
        db.Sessions.Add(session);
        db.HallAttendances.Add(new HallAttendance
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            HallId = hall.Id,
            UserId = visitorId,
            Method = AttendanceMethod.QrScan,
            EnterUtc = session.StartUtc,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return session.Id;
    }

    // Seeds a just-ended session with only a SEAT BOOKING (no hall check-in) for
    // the visitor — the "booked but absent" case that must NOT be prompted.
    private async Task<Guid> SeedEndedSessionWithBookingOnlyAsync(DateTimeOffset endUtc, Guid visitorId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var (hall, session) = BuildEndedSession(endUtc);
        db.Halls.Add(hall);
        db.Sessions.Add(session);
        db.SeatReservations.Add(new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            RowLabel = "A",
            SeatNumber = 1,
            Kind = SeatReservationKind.UserBooking,
            ReservedForUserId = visitorId,
            CreatedByUserId = visitorId,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return session.Id;
    }

    private static (Hall Hall, Session Session) BuildEndedSession(DateTimeOffset endUtc)
    {
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall",
            NameArabic = "قاعة",
            Capacity = 10,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Keynote",
            TitleArabic = "الكلمة الرئيسية",
            HallId = hall.Id,
            StartUtc = endUtc.AddHours(-1),
            EndUtc = endUtc,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        return (hall, session);
    }
}
