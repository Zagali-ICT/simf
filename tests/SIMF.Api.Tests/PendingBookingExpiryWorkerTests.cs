using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common.Enums;
using SIMF.Domain.Programme;
using SIMF.Domain.SeatReservations;
using SIMF.Infrastructure.Operations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// M-6 — tests for the Pending-hold expiry scan. Exercises the internal
/// <see cref="PendingBookingExpiryWorker.RunExpiryScanAsync"/> directly
/// (InternalsVisibleTo) so the once-past-window release and the "leave everything
/// else alone" invariants are covered without driving the BackgroundService loop.
/// </summary>
public sealed class PendingBookingExpiryWorkerTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public PendingBookingExpiryWorkerTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Expiry_scan_releases_only_past_pending_holds()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = await SeedSessionAsync();

        // (a) Pending hold, already past its window → should be released.
        var pastId = await SeedReservationAsync(sessionId, "A", 1,
            SeatReservationKind.UserBooking, BookingStatus.Pending,
            expiresUtc: now.AddHours(-1));
        // (b) Pending hold, window still in the future → untouched.
        var futureId = await SeedReservationAsync(sessionId, "A", 2,
            SeatReservationKind.UserBooking, BookingStatus.Pending,
            expiresUtc: now.AddHours(1));
        // (c) Approved hold, no expiry stamp → untouched.
        var approvedId = await SeedReservationAsync(sessionId, "A", 3,
            SeatReservationKind.UserBooking, BookingStatus.Approved,
            expiresUtc: null);
        // (d) Already-cancelled/released row → untouched.
        var cancelledId = await SeedReservationAsync(sessionId, "A", 4,
            SeatReservationKind.UserBooking, BookingStatus.Cancelled,
            expiresUtc: now.AddHours(-2), releasedAt: now.AddHours(-2));

        int released;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            released = await PendingBookingExpiryWorker.RunExpiryScanAsync(
                db, now, CancellationToken.None);
        }
        Assert.Equal(1, released);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

            var past = await db.SeatReservations.SingleAsync(r => r.Id == pastId);
            Assert.NotNull(past.ReleasedAt);
            Assert.Equal(BookingStatus.Cancelled, past.Status);

            var future = await db.SeatReservations.SingleAsync(r => r.Id == futureId);
            Assert.Null(future.ReleasedAt);
            Assert.Equal(BookingStatus.Pending, future.Status);

            var approved = await db.SeatReservations.SingleAsync(r => r.Id == approvedId);
            Assert.Null(approved.ReleasedAt);
            Assert.Equal(BookingStatus.Approved, approved.Status);

            var cancelled = await db.SeatReservations.SingleAsync(r => r.Id == cancelledId);
            Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        }

        // The freed seat A1 is now bookable again — the filtered unique active-seat
        // index excludes the released row, so a fresh active reservation inserts.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            db.SeatReservations.Add(new SeatReservation
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                RowLabel = "A",
                SeatNumber = 1,
                Kind = SeatReservationKind.UserBooking,
                ReservedForUserId = Guid.NewGuid(),
                CreatedByUserId = Guid.NewGuid(),
                CreatedAt = now,
                Status = BookingStatus.Pending,
                ExpiresUtc = now.AddHours(24),
            });
            // Succeeds — no active reservation holds A1 after the expiry.
            await db.SaveChangesAsync();
        }
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task<Guid> SeedSessionAsync()
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
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Live",
            TitleArabic = "مباشر",
            HallId = hall.Id,
            StartUtc = DateTimeOffset.UtcNow.AddHours(1),
            EndUtc = DateTimeOffset.UtcNow.AddHours(2),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task<Guid> SeedReservationAsync(
        Guid sessionId, string row, int seat,
        SeatReservationKind kind, BookingStatus status,
        DateTimeOffset? expiresUtc, DateTimeOffset? releasedAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var reservation = new SeatReservation
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            RowLabel = row,
            SeatNumber = seat,
            Kind = kind,
            ReservedForUserId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            Status = status,
            ExpiresUtc = expiresUtc,
            ReleasedAt = releasedAt,
        };
        db.SeatReservations.Add(reservation);
        await db.SaveChangesAsync();
        return reservation.Id;
    }
}
