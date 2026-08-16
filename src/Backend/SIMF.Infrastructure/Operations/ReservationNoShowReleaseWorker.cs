// Tests: SIMF.Api.Tests/ReservationNoShowReleaseWorkerTests.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMF.Application.Operations;
using SIMF.Application.SeatReservations.Abstractions;
using SIMF.Common;

namespace SIMF.Infrastructure.Operations;

/// <summary>
/// #6/#17 (owner 2026-07-20, FR-503/903) — background worker that frees seats
/// abandoned by no-shows. Once per minute it asks the seat service to release every
/// ACTIVE (confirmed, still-held) visitor reservation whose no-show deadline (the
/// session's <c>Start − 3min</c>, stamped on <c>NoShowReleaseAt</c> at booking) has
/// passed and whose holder never checked in — so the seat can go to someone else
/// ("cancelled if you don't check in 3 minutes before start"). The release rule
/// itself lives in <see cref="ISeatReservationService.ReleaseNoShowsAsync"/>
/// (unit-tested there); this type only schedules it. Mirrors
/// <see cref="SessionReminderWorker"/>.
/// </summary>
internal sealed class ReservationNoShowReleaseWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IWorkerHeartbeatRegistry heartbeat,
    ILogger<ReservationNoShowReleaseWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    // First tick delayed so the host finishes migrations + seeding before the
    // worker hits the DB — mirrors SessionReminderWorker.
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "ReservationNoShowReleaseWorker started (first poll in {Delay}s, then every {Interval}s).",
            (int)StartupDelay.TotalSeconds, (int)PollInterval.TotalSeconds);

        heartbeat.Register(
            nameof(ReservationNoShowReleaseWorker),
            "Releases seats reserved by no-shows 3 minutes before start.",
            PollInterval);

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
                heartbeat.RecordSuccess(nameof(ReservationNoShowReleaseWorker));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                heartbeat.RecordFailure(nameof(ReservationNoShowReleaseWorker), ex.Message);
                logger.LogError(ex, "ReservationNoShowReleaseWorker tick failed.");
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
        var service = scope.ServiceProvider.GetRequiredService<ISeatReservationService>();

        var released = await service.ReleaseNoShowsAsync(
            timeProvider.SimfNow(), cancellationToken);
        if (released > 0)
        {
            logger.LogInformation(
                "ReservationNoShowReleaseWorker released {Count} no-show hold(s).", released);
        }
    }
}
