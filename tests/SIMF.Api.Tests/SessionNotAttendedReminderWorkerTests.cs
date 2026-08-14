// FR-903 (register defect `FR-903-not-attended-reminder`) — the "session started
// but you have not attended" half never shipped: NotificationKind had no
// not-attended value across 0-58, and ReservationNoShowReleaseWorker (the only
// worker reasoning about no-shows) frees the seat and notifies nobody.
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
using SIMF.Common;

namespace SIMF.Api.Tests;

/// <summary>
/// Tests for the FR-903 not-attended scan. Drives the internal
/// <see cref="SessionNotAttendedReminderWorker.RunNotAttendedScanAsync"/> directly
/// (InternalsVisibleTo), mirroring <see cref="SessionReminderWorkerTests"/>.
/// </summary>
public sealed class SessionNotAttendedReminderWorkerTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public SessionNotAttendedReminderWorkerTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Booked_attendee_who_has_not_arrived_is_nudged_exactly_once()
    {
        var now = SimfClock.Now;
        var visitorId = await SeedVisitorAsync();
        // Started 15 minutes ago (past the 10-minute grace), still running.
        var sessionId = await SeedSessionWithSeatAsync(now.AddMinutes(-15), visitorId);

        var firstPass = await RunScanAsync(now);
        Assert.True(firstPass >= 1);
        Assert.Equal(1, await NudgeCountAsync(visitorId, sessionId));

        // Second pass inside the same window: the D-713 dispatcher guard makes the
        // scan idempotent, so no second nudge — no stamp column needed.
        await RunScanAsync(now);
        Assert.Equal(1, await NudgeCountAsync(visitorId, sessionId));
    }

    [Fact]
    public async Task Attendee_who_has_arrived_is_not_nudged()
    {
        var now = SimfClock.Now;
        var visitorId = await SeedVisitorAsync();
        var sessionId = await SeedSessionWithSeatAsync(now.AddMinutes(-15), visitorId);
        await SeedArrivalAsync(sessionId, visitorId, now.AddMinutes(-12));

        await RunScanAsync(now);

        Assert.Equal(0, await NudgeCountAsync(visitorId, sessionId));
    }

    [Fact]
    public async Task Attendee_who_arrived_and_already_left_is_not_nudged()
    {
        // A closed HallAttendance row is still evidence of attendance — someone who
        // came and went did not fail to attend.
        var now = SimfClock.Now;
        var visitorId = await SeedVisitorAsync();
        var sessionId = await SeedSessionWithSeatAsync(now.AddMinutes(-15), visitorId);
        await SeedArrivalAsync(sessionId, visitorId, now.AddMinutes(-14), now.AddMinutes(-12));

        await RunScanAsync(now);

        Assert.Equal(0, await NudgeCountAsync(visitorId, sessionId));
    }

    [Fact]
    public async Task Session_still_inside_the_arrival_grace_is_not_swept()
    {
        var now = SimfClock.Now;
        var visitorId = await SeedVisitorAsync();
        // Started 2 minutes ago — well inside the 10-minute grace.
        var sessionId = await SeedSessionWithSeatAsync(now.AddMinutes(-2), visitorId);

        await RunScanAsync(now);

        Assert.Equal(0, await NudgeCountAsync(visitorId, sessionId));
    }

    [Fact]
    public async Task Session_that_has_already_ended_is_not_swept()
    {
        // Nudging someone to attend a finished session is noise, not a reminder.
        var now = SimfClock.Now;
        var visitorId = await SeedVisitorAsync();
        var sessionId = await SeedSessionWithSeatAsync(
            now.AddMinutes(-15), visitorId, durationMinutes: 5);

        await RunScanAsync(now);

        Assert.Equal(0, await NudgeCountAsync(visitorId, sessionId));
    }

    [Fact]
    public async Task Released_reservation_is_not_nudged()
    {
        var now = SimfClock.Now;
        var visitorId = await SeedVisitorAsync();
        var sessionId = await SeedSessionWithSeatAsync(
            now.AddMinutes(-15), visitorId, releasedAt: now.AddMinutes(-13));

        await RunScanAsync(now);

        Assert.Equal(0, await NudgeCountAsync(visitorId, sessionId));
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<int> RunScanAsync(DateTime now)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
        return await SessionNotAttendedReminderWorker.RunNotAttendedScanAsync(
            db, dispatcher, now,
            SessionNotAttendedReminderWorker.ArrivalGrace,
            SessionNotAttendedReminderWorker.ReminderWindow,
            NullLogger.Instance, CancellationToken.None);
    }

    private async Task<int> NudgeCountAsync(Guid userId, Guid sessionId)
    {
        using var scope = _factory.Services.CreateScope();
        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return await idDb.Notifications.CountAsync(n =>
            n.Kind == NotificationKind.SessionNotAttended
            && n.RelatedEntityId == sessionId
            && n.UserId == userId);
    }

    private async Task<Guid> SeedVisitorAsync()
    {
        var email = $"notattended-visitor-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Absent Visitor",
            UserType = UserType.Visitor,
            AccountState = AccountState.Approved,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<Guid> SeedSessionWithSeatAsync(
        DateTime start,
        Guid visitorId,
        int durationMinutes = 60,
        DateTime? releasedAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall",
            NameArabic = "قاعة",
            Capacity = 10,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Keynote",
            TitleArabic = "الكلمة الرئيسية",
            HallId = hall.Id,
            Start = start,
            End = start.AddMinutes(durationMinutes),
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        var attendeeProfileId = await TestAttendeeProfiles.EnsureForAccountAsync(db, visitorId);
        db.SeatReservations.Add(new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            RowLabel = "A",
            SeatNumber = 1,
            Kind = SeatReservationKind.UserBooking,
            ReservedForProfileId = attendeeProfileId,
            CreatedByUserId = visitorId,
            ReleasedAt = releasedAt,
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task SeedArrivalAsync(
        Guid sessionId, Guid userId, DateTime enter, DateTime? leave = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hallId = await db.Sessions
            .Where(s => s.Id == sessionId)
            .Select(s => s.HallId)
            .SingleAsync();
        var attendeeProfileId = await TestAttendeeProfiles.EnsureForAccountAsync(db, userId);
        db.HallAttendances.Add(new HallAttendance
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            HallId = hallId,
            UserProfileId = attendeeProfileId,
            Method = AttendanceMethod.QrScan,
            Enter = enter,
            Leave = leave,
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
    }
}
