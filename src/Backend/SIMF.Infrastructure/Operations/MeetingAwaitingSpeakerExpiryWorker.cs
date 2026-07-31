// Tests: SIMF.Api.Tests/MeetingAwaitingSpeakerExpiryWorkerTests.cs
//        SIMF.Api.Tests/SpeakerMeetingQaTests.cs (QA A29 revert notification)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Notifications;
using SIMF.Application.Operations;
using SIMF.Common.Enums;
using SIMF.Domain.BusinessMeetings;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

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
///
/// <para>B10 — the same sweep now also covers DELEGATION meeting requests
/// (<see cref="RunDelegationExpiryScanAsync"/>), which hold a hall slot in
/// AwaitingSpeaker exactly like a speaker request but were never expired.</para>
/// </summary>
internal sealed class MeetingAwaitingSpeakerExpiryWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IWorkerHeartbeatRegistry heartbeat,
    ILogger<MeetingAwaitingSpeakerExpiryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "MeetingAwaitingSpeakerExpiryWorker started (first poll in {Delay}s, then every {Interval}s).",
            (int)StartupDelay.TotalSeconds, (int)PollInterval.TotalSeconds);

        heartbeat.Register(
            nameof(MeetingAwaitingSpeakerExpiryWorker),
            "Reverts stale speaker and delegation meeting requests once their invite links expire.",
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
                heartbeat.RecordSuccess(nameof(MeetingAwaitingSpeakerExpiryWorker));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                heartbeat.RecordFailure(nameof(MeetingAwaitingSpeakerExpiryWorker), ex.Message);
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
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        // MERGE: the speaker scan gained notifications+logger (A29 — tell the requester
        // when a stale AwaitingSpeaker reverts); the delegation scan (B10) is additive
        // beside it. Both run each tick.
        var now = timeProvider.SimfNow();
        var reverted = await RunExpiryScanAsync(
            db, auditLog, notifications, logger, now, cancellationToken);
        var delegationReverted = await RunDelegationExpiryScanAsync(
            db, auditLog, now, cancellationToken);
        if (reverted > 0 || delegationReverted > 0)
        {
            logger.LogInformation(
                "MeetingAwaitingSpeakerExpiryWorker reverted {Count} stale AwaitingSpeaker "
                + "speaker request(s) and {DelegationCount} delegation request(s).",
                reverted, delegationReverted);
        }
    }

    /// <summary>
    /// The core scan, extracted for direct unit testing. Reverts every AwaitingSpeaker
    /// request that has no still-usable token (none unused AND unexpired). Returns the
    /// number reverted.
    /// </summary>
    internal static async Task<int> RunExpiryScanAsync(
        SimfAppDbContext db, IAuditLog auditLog, INotificationDispatcher notifications,
        ILogger logger, DateTime now,
        CancellationToken cancellationToken)
    {
        var stale = await db.SpeakerMeetingRequests
            .Where(r => r.Status == MeetingRequestStatus.AwaitingSpeaker
                && !db.MeetingActionTokens.Any(t => t.SpeakerMeetingRequestId == r.Id
                    && t.UsedAt == null && t.Expires > now))
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
            req.SlotStart = null;
            req.SlotEnd = null;
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

        // QA A29 — the revert used to be completely silent: the requester's proposed
        // time was released and nobody was told. Tell them the speaker did not confirm
        // in time and the request is back with the team. Dispatched AFTER the save so a
        // notify failure can never roll back the revert; best-effort (swallow-and-log).
        // Only the requester is notified — per D-717 owner decision C the CP status flip
        // back into the Pending queue IS the admin signal.
        foreach (var req in stale)
        {
            await NotifyRequesterRevertedAsync(db, notifications, logger, req, cancellationToken);
        }

        return stale.Count;
    }

    private static async Task NotifyRequesterRevertedAsync(
        SimfAppDbContext db, INotificationDispatcher notifications, ILogger logger,
        SpeakerMeetingRequest req, CancellationToken cancellationToken)
    {
        var speakerName = await db.Speakers.AsNoTracking()
            .Where(s => s.Id == req.SpeakerId).Select(s => s.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? "the speaker";

        await notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = req.RequestedByUserId,
            Kind = NotificationKind.MeetingCancelled,
            Title = "Meeting time released",
            TitleArabic = "تم تحرير موعد المقابلة",
            Body = $"{speakerName} did not confirm in time, so the proposed time was "
                + "released. Your request is back with the SIMF team.",
            BodyArabic = $"لم يؤكّد {speakerName} في الوقت المحدّد، لذا تم تحرير الموعد "
                + "المقترح. طلبك عاد إلى فريق الملتقى.",
            Severity = NotificationSeverity.Info,
            RelatedEntityType = nameof(SpeakerMeetingRequest),
            RelatedEntityId = req.Id,
            SendEmail = true,
        }, logger, cancellationToken);
    }

    /// <summary>
    /// B10 — the delegation twin of <see cref="RunExpiryScanAsync"/>. An AwaitingSpeaker
    /// DELEGATION meeting HOLDS its hall slot (<c>MeetingRequestStatuses.SlotHolding</c>)
    /// but nothing ever expired one, so a target delegation that simply never confirmed
    /// held the slot forever and the admin queue dead-ended. Same cadence, same shape and
    /// the same "no still-usable token" rule as the speaker sweep — the D-767 delegation
    /// confirm token carries the same 72h TTL, so this is time-based via that TTL. Reverts
    /// to a clean Pending (which releases the slot) so the admin can re-decide. Extracted
    /// for direct unit testing; returns the number reverted.
    /// </summary>
    internal static async Task<int> RunDelegationExpiryScanAsync(
        SimfAppDbContext db, IAuditLog auditLog, DateTime now,
        CancellationToken cancellationToken)
    {
        var stale = await db.DelegationMeetingRequests
            .Where(r => r.Status == MeetingRequestStatus.AwaitingSpeaker
                && !db.DelegationMeetingActionTokens.Any(
                    t => t.DelegationMeetingRequestId == r.Id
                        && t.UsedAt == null && t.ExpiresUtc > now))
            .ToListAsync(cancellationToken);
        if (stale.Count == 0)
        {
            return 0;
        }

        foreach (var req in stale)
        {
            req.Status = MeetingRequestStatus.Pending;
            req.HallId = null;
            req.MeetingTableId = null;
            req.SlotStart = null;
            req.SlotEnd = null;
            req.AvailabilityWindowId = null;
            req.RespondedAt = null;
            req.RespondedByUserId = null;
            req.ResponseNote = null;

            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.DelegationMeetingRequestReverted,
                Outcome = AuditOutcome.Success,
                ActorUserId = Guid.Empty,
                Detail = $"requestId={req.Id}; reason=AwaitingConfirmTokenExpired",
            }, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return stale.Count;
    }
}
