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
/// P1.7 (D-217) — tests for the automated session-reminder scan. Exercises the
/// internal <see cref="SessionReminderWorker.RunReminderScanAsync"/> directly
/// (InternalsVisibleTo) so the once-only dedup and the lead-window bound are
/// covered without driving the BackgroundService loop.
/// </summary>
public sealed class SessionReminderWorkerTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public SessionReminderWorkerTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Scan_reminds_attendees_of_a_session_in_the_lead_window_exactly_once()
    {
        var now = SimfClock.Now;
        var visitorId = await SeedVisitorAsync();
        var sessionId = await SeedSessionWithSeatAsync(now.AddMinutes(10), visitorId);

        var firstPass = await RunScanAsync(now);
        Assert.True(firstPass >= 1);

        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();

            var session = await appDb.Sessions.SingleAsync(s => s.Id == sessionId);
            Assert.NotNull(session.ReminderSentAt);

            var count = await idDb.Notifications.CountAsync(n =>
                n.Kind == NotificationKind.SessionReminder
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
                n.Kind == NotificationKind.SessionReminder
                && n.RelatedEntityId == sessionId
                && n.UserId == visitorId);
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task Scan_ignores_a_session_beyond_the_lead_window()
    {
        var now = SimfClock.Now;
        var visitorId = await SeedVisitorAsync();
        // Starts in two hours — well beyond the 30-minute lead window.
        var sessionId = await SeedSessionWithSeatAsync(now.AddHours(2), visitorId);

        await RunScanAsync(now);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var session = await appDb.Sessions.SingleAsync(s => s.Id == sessionId);
        // The far-future session is left untouched.
        Assert.Null(session.ReminderSentAt);
    }

    [Fact]
    public async Task Scan_commits_the_dedup_stamp_before_dispatching_so_a_restart_cannot_resend()
    {
        // #12 — the once-only stamp must be committed to SIMF_App BEFORE any
        // notification goes out, so a restart mid-tick re-scan sees the session
        // already claimed and never re-sends. The probe reads the session row
        // from a SEPARATE committed context at the moment of the first dispatch:
        // under the old stamp-after-loop code it would still be null there.
        var now = SimfClock.Now;
        var visitorId = await SeedVisitorAsync();
        await SeedSessionWithSeatAsync(now.AddMinutes(10), visitorId);

        var probe = new StampProbeDispatcher(_factory);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            await SessionReminderWorker.RunReminderScanAsync(
                db, probe, now, SessionReminderWorker.ReminderLeadTime,
                NullLogger.Instance, CancellationToken.None);
        }

        Assert.True(probe.Dispatched);
        Assert.True(probe.StampCommittedAtFirstDispatch,
            "ReminderSentAt must be committed before dispatch so a restart cannot resend.");
    }

    // -- Helpers ---------------------------------------------------------------

    /// <summary>A dispatcher that, on the first notification, reads the session it
    /// is FOR (via <see cref="NotificationRequest.RelatedEntityId"/>) from a fresh
    /// committed context to prove that session's dedup stamp was persisted before
    /// its batch went out (#12 claim-before-dispatch). Keying on the dispatched
    /// session (not a fixed id) keeps it robust when the shared test DB makes more
    /// than one session due.</summary>
    private sealed class StampProbeDispatcher(SimfApiFactory factory) : INotificationDispatcher
    {
        public bool Dispatched { get; private set; }
        public bool StampCommittedAtFirstDispatch { get; private set; }

        public async Task DispatchAsync(
            NotificationRequest request, CancellationToken cancellationToken = default)
        {
            if (Dispatched)
            {
                return;
            }
            Dispatched = true;
            var sessionId = request.RelatedEntityId!.Value;
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var session = await db.Sessions.AsNoTracking()
                .SingleAsync(s => s.Id == sessionId, cancellationToken);
            StampCommittedAtFirstDispatch = session.ReminderSentAt != null;
        }
    }

    private async Task<int> RunScanAsync(DateTime now)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
        return await SessionReminderWorker.RunReminderScanAsync(
            db, dispatcher, now, SessionReminderWorker.ReminderLeadTime,
            NullLogger.Instance, CancellationToken.None);
    }

    private async Task<Guid> SeedVisitorAsync()
    {
        var email = $"reminder-visitor-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Reminder Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<Guid> SeedSessionWithSeatAsync(DateTime start, Guid visitorId)
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
            End = start.AddHours(1),
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
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
        return session.Id;
    }
}
