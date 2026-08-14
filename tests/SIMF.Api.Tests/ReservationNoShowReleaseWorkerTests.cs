using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.SeatReservations.Abstractions;
using SIMF.Common.Enums;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Persistence;
using Xunit;
using SIMF.Common;

namespace SIMF.Api.Tests;

/// <summary>
/// #6/#17 — tests for the no-show release rule
/// (<see cref="ISeatReservationService.ReleaseNoShowsAsync"/>) that
/// <c>ReservationNoShowReleaseWorker</c> runs once per minute. An active hold whose
/// no-show deadline (Start − 3min, on <c>Expires</c>) has passed is released
/// ONLY when its holder never checked in and it was booked ahead of the deadline;
/// a checked-in holder, a future deadline, a walk-in booked after the deadline, an
/// admin block and an already-released row are all left alone.
/// </summary>
public sealed class ReservationNoShowReleaseWorkerTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public ReservationNoShowReleaseWorkerTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Releases_only_past_deadline_no_show_holds_booked_ahead()
    {
        var now = SimfClock.Now;
        var (sessionId, hallId) = await SeedSessionAsync();

        // (a) Approved, deadline passed, booked ahead, NO check-in → released.
        var noShowId = await SeedReservationAsync(sessionId, "A", 1,
            status: BookingStatus.Approved,
            createdAt: now.AddHours(-2), expires: now.AddMinutes(-1));

        // (b) Approved, deadline passed, booked ahead, but the holder CHECKED IN → kept.
        var checkedInUser = Guid.NewGuid();
        var attendeeId = await SeedReservationAsync(sessionId, "A", 2,
            status: BookingStatus.Approved,
            createdAt: now.AddHours(-2), expires: now.AddMinutes(-1),
            reservedForUserId: checkedInUser);
        await SeedCheckInAsync(sessionId, hallId, checkedInUser, now.AddMinutes(-5));

        // (c) Approved, deadline still in the future → untouched.
        var futureId = await SeedReservationAsync(sessionId, "A", 3,
            status: BookingStatus.Approved,
            createdAt: now.AddHours(-2), expires: now.AddMinutes(30));

        // (d) Approved, deadline passed, but a WALK-IN booked after it (CreatedAt >=
        //     Expires) → exempt (they are present, not a no-show).
        var walkInId = await SeedReservationAsync(sessionId, "A", 4,
            status: BookingStatus.Approved,
            createdAt: now, expires: now.AddMinutes(-1));

        // (e) Admin block (no holder, no expiry) → never touched.
        var adminId = await SeedReservationAsync(sessionId, "B", 1,
            status: BookingStatus.Approved,
            createdAt: now.AddHours(-2), expires: null,
            kind: SeatReservationKind.AdminReservedRow);

        // (f) Already cancelled/released → untouched.
        var goneId = await SeedReservationAsync(sessionId, "B", 2,
            status: BookingStatus.Cancelled,
            createdAt: now.AddHours(-2), expires: now.AddMinutes(-1),
            releasedAt: now.AddMinutes(-1));

        int released;
        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<ISeatReservationService>();
            released = await svc.ReleaseNoShowsAsync(now, CancellationToken.None);
        }
        Assert.Equal(1, released);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

            var noShow = await db.SeatReservations.SingleAsync(r => r.Id == noShowId);
            Assert.NotNull(noShow.ReleasedAt);
            Assert.Equal(BookingStatus.Cancelled, noShow.Status);

            foreach (var keptId in new[] { attendeeId, futureId, walkInId, adminId })
            {
                var kept = await db.SeatReservations.SingleAsync(r => r.Id == keptId);
                Assert.Null(kept.ReleasedAt);
                Assert.Equal(BookingStatus.Approved, kept.Status);
            }

            var gone = await db.SeatReservations.SingleAsync(r => r.Id == goneId);
            Assert.Equal(BookingStatus.Cancelled, gone.Status);
        }
    }

    [Fact]
    public async Task Notifies_the_freed_no_show_holder()
    {
        var now = SimfClock.Now;
        var (sessionId, _) = await SeedSessionAsync();
        // A real Identity user — the Notification row FKs to the user, so a random
        // id would make the (swallowed) dispatch fail silently.
        var holder = await CreateApprovedUserAsync();
        await SeedReservationAsync(sessionId, "A", 1,
            status: BookingStatus.Approved,
            createdAt: now.AddHours(-2), expires: now.AddMinutes(-1),
            reservedForUserId: holder);

        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<ISeatReservationService>();
            Assert.Equal(1, await svc.ReleaseNoShowsAsync(now, CancellationToken.None));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var note = await db.Notifications.SingleOrDefaultAsync(
                n => n.UserId == holder && n.Kind == NotificationKind.BookingReleased);
            Assert.NotNull(note);
        }
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task<Guid> CreateApprovedUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var email = $"ns-{Guid.NewGuid():N}@simf.test";
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "No Show",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<(Guid SessionId, Guid HallId)> SeedSessionAsync()
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
            Title = "Live",
            TitleArabic = "مباشر",
            HallId = hall.Id,
            Start = SimfClock.Now.AddMinutes(2),
            End = SimfClock.Now.AddHours(2),
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return (session.Id, hall.Id);
    }

    private async Task<Guid> SeedReservationAsync(
        Guid sessionId, string? row, int? seat,
        BookingStatus status, DateTime createdAt, DateTime? expires,
        Guid? reservedForUserId = default, DateTime? releasedAt = null,
        SeatReservationKind kind = SeatReservationKind.UserBooking)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var holder = kind == SeatReservationKind.AdminReservedRow
            ? (Guid?)null
            : reservedForUserId ?? Guid.NewGuid();
        var reservation = new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            RowLabel = row,
            SeatNumber = seat,
            Kind = kind,
            ReservedForProfileId =
                await TestAttendeeProfiles.EnsureForOptionalAccountAsync(db, holder),
            CreatedByUserId = holder ?? Guid.NewGuid(),
            CreatedAt = createdAt,
            Status = status,
            Expires = expires,
            ReleasedAt = releasedAt,
        };
        db.SeatReservations.Add(reservation);
        await db.SaveChangesAsync();
        return reservation.Id;
    }

    private async Task SeedCheckInAsync(
        Guid sessionId, Guid hallId, Guid userId, DateTime enter)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var attendeeProfileId = await TestAttendeeProfiles.EnsureForAccountAsync(db, userId);
        db.HallAttendances.Add(new HallAttendance
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            HallId = hallId,
            UserProfileId = attendeeProfileId,
            Method = AttendanceMethod.QrScan,
            Enter = enter,
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
    }
}
