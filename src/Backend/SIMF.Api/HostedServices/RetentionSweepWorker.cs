using SIMF.Application.IdentityAccess.Abstractions;

namespace SIMF.Api.HostedServices;

/// <summary>
/// A4 (NCA data-minimisation) — runs the security-artifact retention purge once
/// a day. Modeled on <see cref="DormantAccountSweepService"/>: the first tick is
/// one interval after start, so a short-lived test host never triggers it, and a
/// failed tick is logged and retried next interval rather than crashing the host.
/// </summary>
internal sealed class RetentionSweepWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<RetentionSweepWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRetentionPurgeService>();
                await service.PurgeExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Host is shutting down.
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Retention purge failed; will retry next interval.");
            }
        }
    }
}
