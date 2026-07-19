using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Application.Operations;

namespace SIMF.Api.HostedServices;

/// <summary>
/// A1-19 (NCA) — runs the dormant-account disable sweep once a day. The sweep
/// itself is a no-op until <c>IdentityLifecycle:DormantAccountDisableDays</c> is
/// configured greater than zero, so registering this worker is always safe. The
/// first tick is one interval after start, so a short-lived test host never
/// triggers it.
/// </summary>
internal sealed class DormantAccountSweepService(
    IServiceScopeFactory scopeFactory,
    IWorkerHeartbeatRegistry heartbeat,
    ILogger<DormantAccountSweepService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        heartbeat.Register(
            nameof(DormantAccountSweepService),
            "Disables dormant accounts once a day.",
            Interval);
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IDormantAccountService>();
                await service.DisableDormantAccountsAsync(stoppingToken);
                heartbeat.RecordSuccess(nameof(DormantAccountSweepService));
            }
            catch (OperationCanceledException)
            {
                // Host is shutting down.
            }
            catch (Exception ex)
            {
                heartbeat.RecordFailure(nameof(DormantAccountSweepService), ex.Message);
                logger.LogError(ex, "Dormant-account sweep failed; will retry next interval.");
            }
        }
    }
}
