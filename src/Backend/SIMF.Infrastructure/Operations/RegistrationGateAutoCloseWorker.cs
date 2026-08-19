// Tests: SIMF.Api.Tests/OperationsTogglesTests.cs (firing clears the spent
//        schedule, so a re-open that echoes the gate back actually re-opens it)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Operations;
using SIMF.Common.Enums;
using SIMF.Domain.Operations;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

namespace SIMF.Infrastructure.Operations;

/// <summary>
/// Background worker that flips
/// <see cref="RegistrationGate.IsOpen"/> to false the first time
/// <see cref="RegistrationGate.AutoClose"/> passes. Runs once per
/// minute; cheap query on a single-row table.
///
/// <para>The worker does not race admins: it writes only when IsOpen is still
/// true AND AutoClose &lt;= now.</para>
///
/// <para>Firing CLEARS AutoClose as well as flipping IsOpen. A spent schedule is
/// a date that has happened, not a pending instruction, and leaving it behind
/// made registration impossible to re-open: the CP form pre-fills AutoClose from
/// the gate and posts it back verbatim, so an admin re-opening sent
/// <c>IsOpen=true</c> with a date already in the past. The gate then read as Open
/// in the CP while <c>IsRegistrationOpenAsync</c> still rejected every sign-up,
/// and this worker flipped IsOpen back within the minute — unless the admin
/// happened to notice and blank the date field by hand.</para>
/// </summary>
internal sealed class RegistrationGateAutoCloseWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IWorkerHeartbeatRegistry heartbeat,
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

        heartbeat.Register(
            nameof(RegistrationGateAutoCloseWorker),
            "Closes registration when its scheduled auto-close time passes.",
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
                heartbeat.RecordSuccess(nameof(RegistrationGateAutoCloseWorker));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                heartbeat.RecordFailure(nameof(RegistrationGateAutoCloseWorker), ex.Message);
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

        var closedAt = await RunAutoCloseScanAsync(
            db, auditLog, timeProvider.SimfNow(), cancellationToken);
        if (closedAt is { } closeAt)
        {
            logger.LogInformation(
                "RegistrationGate auto-closed (AutoClose {CloseAt} passed).", closeAt);
        }
    }

    /// <summary>The core check, extracted for direct unit testing. Returns the
    /// schedule that fired, or null when there was nothing to close.</summary>
    internal static async Task<DateTime?> RunAutoCloseScanAsync(
        SimfAppDbContext db, IAuditLog auditLog, DateTime now,
        CancellationToken cancellationToken)
    {
        var row = await db.RegistrationGate
            .SingleOrDefaultAsync(g => g.Id == RegistrationGate.SingletonId, cancellationToken);
        if (row is null) { return null; }
        if (!row.IsOpen) { return null; }
        if (row.AutoClose is not { } closeAt) { return null; }
        if (closeAt > now) { return null; }

        row.IsOpen = false;
        // The schedule has been spent — clear it (see the class remarks: leaving
        // the past date behind is what made the gate impossible to re-open).
        row.AutoClose = null;
        row.LastChangedAt = now;
        row.LastChangedByUserId = null; // worker, not a person
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.RegistrationGateAutoClosed,
            null,
            $"autoClose={closeAt:O}",
            cancellationToken);

        return closeAt;
    }
}
