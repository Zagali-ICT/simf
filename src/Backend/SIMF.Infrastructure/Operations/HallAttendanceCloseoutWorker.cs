// Tests: SIMF.Api.Tests/Operations/HallAttendanceCloseoutWorkerTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMF.Application.Operations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Operations;

/// <summary>
/// G-2 (chain reconciliation) — closes hall-attendance rows that never received a
/// departure. A hall-door gate in In-only mode (or an attendee who simply never
/// scanned out) leaves an open <c>HallAttendance</c> row; without a sweep the
/// session's attendance count only ever grows. Once per minute this closes every
/// open row (Leave null) whose session has ended, stamping
/// <c>Leave = Session.End</c> so attendance reflects the true window.
/// Mirrors <c>SessionRatingPromptWorker</c>'s poll/startup/error shape.
/// </summary>
internal sealed class HallAttendanceCloseoutWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IWorkerHeartbeatRegistry heartbeat,
    ILogger<HallAttendanceCloseoutWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        heartbeat.Register(
            nameof(HallAttendanceCloseoutWorker),
            "Closes hall-attendance rows whose session has ended.",
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
                await CloseOnceAsync(stoppingToken);
                heartbeat.RecordSuccess(nameof(HallAttendanceCloseoutWorker));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                heartbeat.RecordFailure(nameof(HallAttendanceCloseoutWorker), ex.Message);
                logger.LogError(ex, "HallAttendanceCloseoutWorker tick failed.");
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

    private async Task CloseOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var closed = await CloseEndedSessionsAsync(db, timeProvider.GetUtcNow(), cancellationToken);
        if (closed > 0)
        {
            logger.LogInformation(
                "HallAttendanceCloseoutWorker closed {Count} stale attendance row(s).", closed);
        }
    }

    /// <summary>Closes every open attendance row whose session has ended, setting
    /// Leave to the session's End. Extracted for direct unit testing.</summary>
    internal static async Task<int> CloseEndedSessionsAsync(
        SimfAppDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var open = await db.HallAttendances
            .Where(a => a.Leave == null && a.Session!.End <= now)
            .Select(a => new { Row = a, a.Session!.End })
            .ToListAsync(cancellationToken);
        if (open.Count == 0) { return 0; }

        foreach (var item in open)
        {
            item.Row.Leave = item.End;
            item.Row.UpdatedAt = now;
        }
        await db.SaveChangesAsync(cancellationToken);
        return open.Count;
    }
}
