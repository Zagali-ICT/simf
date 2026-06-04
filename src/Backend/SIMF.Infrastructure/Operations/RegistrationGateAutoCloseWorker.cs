using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Common.Enums;
using SIMF.Domain.Operations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Operations;

/// <summary>
/// D-166 (gap doc G4, PDF §2.3) — background worker that flips
/// <see cref="RegistrationGate.IsOpen"/> to false the first time
/// <see cref="RegistrationGate.AutoCloseUtc"/> passes. Runs once per
/// minute; cheap query on a single-row table.
///
/// <para>The worker does not race admins: it writes only when IsOpen
/// is still true AND AutoCloseUtc &lt;= now. A concurrent admin
/// re-open simply clears AutoCloseUtc, after which the worker has
/// nothing to flip.</para>
/// </summary>
internal sealed class RegistrationGateAutoCloseWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<RegistrationGateAutoCloseWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    // First tick is delayed so the host has time to finish migrations and
    // seeding before the worker hits the DB; without it, integration-test
    // fixtures race the worker's first SELECT against EnsureDatabaseCreated.
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "RegistrationGateAutoCloseWorker started (first poll in {Delay}s, then every {Interval}s).",
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
                logger.LogError(ex, "RegistrationGateAutoCloseWorker tick failed.");
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
        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLog>();

        var row = await db.RegistrationGate
            .SingleOrDefaultAsync(g => g.Id == RegistrationGate.SingletonId, cancellationToken);
        if (row is null) { return; }
        if (!row.IsOpen) { return; }
        if (row.AutoCloseUtc is not { } closeAt) { return; }
        if (closeAt > timeProvider.GetUtcNow()) { return; }

        row.IsOpen = false;
        row.LastChangedAt = timeProvider.GetUtcNow();
        row.LastChangedByUserId = null; // worker, not a person
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.RegistrationGateAutoClosed,
            Outcome = AuditOutcome.Success,
            ActorUserId = null,
            Detail = $"autoCloseUtc={closeAt:O}",
        }, cancellationToken);

        logger.LogInformation(
            "RegistrationGate auto-closed (AutoCloseUtc {CloseAt} passed).", closeAt);
    }
}
