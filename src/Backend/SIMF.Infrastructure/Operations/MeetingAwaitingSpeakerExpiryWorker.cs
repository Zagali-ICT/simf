// Tests: SIMF.Api.Tests/MeetingAwaitingSpeakerExpiryWorkerTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Common.Enums;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Operations;

/// <summary>
/// R-1 (on-site remediation) — reverts a stuck <see cref="MeetingRequestStatus.AwaitingSpeaker"/>
/// speaker meeting request back to <see cref="MeetingRequestStatus.Pending"/> once its
/// double-opt-in tokens can no longer be used (all expired or consumed) so the admin queue
/// never dead-ends when a speaker never clicks the 72h link. Reverting frees the held hall
/// slot (Pending is not a slot-holding state) and clears the hall binding so the admin can
/// re-decide cleanly. The requester saw the row as "under review" (AwaitingSpeaker folds to
/// Pending on the app feed) the whole time, so nothing changes for them. Mirrors
/// <see cref="SessionReminderWorker"/>'s scoped-poll shape.
/// </summary>
internal sealed class MeetingAwaitingSpeakerExpiryWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<MeetingAwaitingSpeakerExpiryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "MeetingAwaitingSpeakerExpiryWorker started (first poll in {Delay}s, then every {Interval}s).",
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
                logger.LogError(ex, "MeetingAwaitingSpeakerExpiryWorker tick failed.");
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

        var reverted = await RunExpiryScanAsync(
            db, auditLog, timeProvider.GetUtcNow(), cancellationToken);
        if (reverted > 0)
        {
            logger.LogInformation(
                "MeetingAwaitingSpeakerExpiryWorker reverted {Count} stale AwaitingSpeaker request(s).",
                reverted);
        }
    }

    /// <summary>
    /// The core scan, extracted for direct unit testing. Reverts every AwaitingSpeaker
    /// request that has no still-usable token (none unused AND unexpired). Returns the
    /// number reverted.
    /// </summary>
    internal static async Task<int> RunExpiryScanAsync(
        SimfAppDbContext db, IAuditLog auditLog, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var stale = await db.SpeakerMeetingRequests
            .Where(r => r.Status == MeetingRequestStatus.AwaitingSpeaker
                && !db.MeetingActionTokens.Any(t => t.SpeakerMeetingRequestId == r.Id
                    && t.UsedAt == null && t.ExpiresUtc > now))
            .ToListAsync(cancellationToken);
        if (stale.Count == 0)
        {
            return 0;
        }

        foreach (var req in stale)
        {
            // Back to a clean Pending: drop the hall binding (frees the slot) and the
            // response stamp so the admin re-decides from scratch. AvailabilityWindowId
            // is nulled alongside the slot (D-611/D-612 FK) so a reverted VIP-slot
            // request is not left pointing at a window with no slot.
            req.Status = MeetingRequestStatus.Pending;
            req.HallId = null;
            req.MeetingTableId = null;
            req.SlotStartUtc = null;
            req.SlotEndUtc = null;
            req.AvailabilityWindowId = null;
            req.SpeakerDecisionAt = null;
            req.RespondedAt = null;
            req.RespondedByUserId = null;
            req.ResponseNote = null;

            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.SpeakerMeetingRequestReverted,
                Outcome = AuditOutcome.Success,
                ActorUserId = Guid.Empty,
                Detail = $"requestId={req.Id}; reason=AwaitingSpeakerTokensExpired",
            }, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return stale.Count;
    }
}
