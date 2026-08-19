// Tests: SIMF.Api.Tests/NotificationBroadcastTests.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMF.Application.Notifications;
using SIMF.Application.Operations;

namespace SIMF.Infrastructure.Operations;

/// <summary>
/// Drains the admin notification-broadcast queue (Control Panel "Announcements"
/// desk). Every poll it claims + fans out each Pending broadcast — one in-app row
/// + one queued email per recipient — via <see cref="INotificationBroadcastService"/>,
/// processing all that are ready in one tick so a burst does not wait a full poll
/// between each. The service claims a row (Pending -> Processing) before dispatch,
/// so a restart mid-send never re-sends (at-most-once). Fan-out is paced against
/// the bounded email queue inside the service, so a large "all users" send never
/// overruns it.
/// </summary>
internal sealed class NotificationBroadcastWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IWorkerHeartbeatRegistry heartbeat,
    ILogger<NotificationBroadcastWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    // First tick is delayed so the host finishes migrations + seeding before the
    // worker hits the DB — mirrors the other operational workers.
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "NotificationBroadcastWorker started (first poll in {Delay}s, then every {Interval}s).",
            (int)StartupDelay.TotalSeconds, (int)PollInterval.TotalSeconds);

        heartbeat.Register(
            nameof(NotificationBroadcastWorker),
            "Fans out admin notification broadcasts (in-app + email) to their recipients.",
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
                // Reconcile before draining, EVERY tick rather than once at startup.
                // A restart 30 seconds into a fan-out leaves a Processing row younger
                // than the stalled cutoff, which the startup-only pass skipped; the
                // drain loop only ever picks Pending, so that row sat Processing with
                // Dispatched=0 until the NEXT restart happened to catch it while it
                // was old enough. Running the sweep each poll closes it within one
                // cutoff instead.
                await RecoverStalledAsync(stoppingToken);
                var sent = await DrainAsync(stoppingToken);
                heartbeat.RecordSuccess(nameof(NotificationBroadcastWorker));
                if (sent > 0)
                {
                    logger.LogInformation(
                        "NotificationBroadcastWorker sent {Count} broadcast(s).", sent);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                heartbeat.RecordFailure(nameof(NotificationBroadcastWorker), ex.Message);
                logger.LogError(ex, "NotificationBroadcastWorker tick failed.");
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

    // Per-tick reconciliation: mark any broadcast left Processing by a prior crash
    // mid-send as Failed so it surfaces in history instead of hanging. The service
    // only touches rows whose StartedAt is older than its stalled cutoff, so a
    // fan-out still legitimately running is not swept.
    private async Task RecoverStalledAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<INotificationBroadcastService>();
            var recovered = await service.RecoverStalledAsync(cancellationToken);
            if (recovered > 0)
            {
                logger.LogWarning(
                    "NotificationBroadcastWorker recovered {Count} stalled broadcast(s) to Failed.",
                    recovered);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NotificationBroadcastWorker reconciliation failed.");
        }
    }

    // Process every Pending broadcast ready right now, one at a time, so a burst of
    // queued broadcasts does not wait a poll interval between each. A fresh scope
    // per broadcast keeps the DbContexts short-lived.
    private async Task<int> DrainAsync(CancellationToken cancellationToken)
    {
        var sent = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<INotificationBroadcastService>();
            if (!await service.ProcessNextPendingAsync(cancellationToken))
            {
                break;
            }
            sent++;
        }
        return sent;
    }
}
