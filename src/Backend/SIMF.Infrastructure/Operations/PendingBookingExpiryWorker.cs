// Tests: SIMF.Api.Tests/PendingBookingExpiryWorkerTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMF.Common.Enums;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Operations;

/// <summary>
/// M-6 — background worker that releases Pending, still-held seat bookings whose
/// hold window (<c>SeatReservation.ExpiresUtc</c>) has passed. A held Pending
/// booking blocks its seat and blocks the visitor from booking an overlapping
/// session; without an expiry, a booking the Control Panel never decides would
/// hold that capacity forever. Once per minute this releases expired holds
/// (ReleasedAt + Status = Cancelled), freeing the seat. Mirrors
/// <see cref="SessionReminderWorker"/>.
/// </summary>
internal sealed class PendingBookingExpiryWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<PendingBookingExpiryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    // First tick delayed so the host finishes migrations + seeding before the
    // worker hits the DB — mirrors SessionReminderWorker.
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "PendingBookingExpiryWorker started (first poll in {Delay}s, then every {Interval}s).",
            (int)StartupDelay.TotalSeconds, (int)PollInterval.TotalSeconds);

        try
        {
            await Task.Delay(StartupDelay, timeProvider, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PendingBookingExpiryWorker tick failed.");
            }
            try
            {
                await Task.Delay(PollInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task CheckOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var released = await RunExpiryScanAsync(
            db, timeProvider.GetUtcNow(), cancellationToken);
        if (released > 0)
        {
            logger.LogInformation(
                "PendingBookingExpiryWorker released {Count} expired hold(s).", released);
        }
    }

    /// <summary>
    /// The core scan, extracted for direct unit testing. Releases every Pending,
    /// still-held booking whose <c>ExpiresUtc</c> is at or before
    /// <paramref name="now"/> (ReleasedAt + Status = Cancelled), freeing the seat.
    /// Returns the number released. Approved/Rejected/Cancelled rows, admin blocks
    /// (ExpiresUtc null) and future-dated holds are left untouched.
    /// </summary>
    internal static async Task<int> RunExpiryScanAsync(
        SimfAppDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var expired = await db.SeatReservations
            .Where(r => r.Status == BookingStatus.Pending
                && r.ReleasedAt == null
                && r.ExpiresUtc != null
                && r.ExpiresUtc <= now)
            .ToListAsync(cancellationToken);
        if (expired.Count == 0)
        {
            return 0;
        }

        foreach (var booking in expired)
        {
            booking.ReleasedAt = now;
            booking.Status = BookingStatus.Cancelled;
        }
        await db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }
}
