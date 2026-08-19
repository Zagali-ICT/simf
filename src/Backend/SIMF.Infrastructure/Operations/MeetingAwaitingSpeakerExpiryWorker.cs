// Tests: SIMF.Api.Tests/MeetingAwaitingSpeakerExpiryWorkerTests.cs
//        SIMF.Api.Tests/SpeakerMeetingQaTests.cs (revert notification)
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
/// Reverts a stuck <see cref="MeetingRequestStatus.AwaitingSpeaker"/>
/// speaker meeting request back to <see cref="MeetingRequestStatus.Pending"/> once its
/// double-opt-in tokens can no longer be used (all expired or consumed) so the admin queue
/// never dead-ends when a speaker never clicks the 72h link. Reverting frees the held hall
/// slot (Pending is not a slot-holding state) and clears the hall binding so the admin can
/// re-decide cleanly. The requester saw the row as "under review" (AwaitingSpeaker folds to
/// Pending on the app feed) the whole time, so nothing changes for them. Mirrors
/// <see cref="SessionReminderWorker"/>'s scoped-poll shape.
///
/// <para>The same sweep also covers DELEGATION meeting requests
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

        // The speaker scan takes notifications+logger so it can tell the requester
        // when a stale AwaitingSpeaker reverts; the delegation scan sits beside it
        // and needs neither. Both run each tick.
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
    /// number actually reverted, which is not the number found stale: a request the
    /// other party confirmed between the load and the write is left alone.
    /// </summary>
    internal static async Task<int> RunExpiryScanAsync(
        SimfAppDbContext db, IAuditLog auditLog, INotificationDispatcher notifications,
        ILogger logger, DateTime now,
        CancellationToken cancellationToken)
    {
        // AsNoTracking + projection: the revert below is a conditional UPDATE, so
        // nothing here may stay tracked and get written back without its guard.
        var stale = await db.SpeakerMeetingRequests
            .AsNoTracking()
            .Where(r => r.Status == MeetingRequestStatus.AwaitingSpeaker
                && !db.MeetingActionTokens.Any(t => t.SpeakerMeetingRequestId == r.Id
                    && t.UsedAt == null && t.Expires > now))
            .Select(r => new StaleRequest(r.Id, r.RequestedByUserId, r.SpeakerId))
            .ToListAsync(cancellationToken);
        if (stale.Count == 0)
        {
            return 0;
        }

        var reverted = new List<StaleRequest>(stale.Count);
        foreach (var req in stale)
        {
            // Back to a clean Pending: drop the hall binding (frees the slot) and the
            // response stamp so the admin re-decides from scratch. AvailabilityWindowId
            // is nulled alongside the slot so a reverted VIP-slot
            // request is not left pointing at a window with no slot.
            //
            // Guarded on Status, the same conditional-claim pattern
            // ConfirmByOtherPartyAsync and MeetingReminderWorker use. The token
            // expires at 72h but the in-app confirm does not, so a request can be in
            // this stale set AND still tap-confirmable: the plain UPDATE ... WHERE Id
            // this replaces would overwrite an Accept that landed a few milliseconds
            // into the tick, silently un-booking a meeting whose confirmation email
            // had already gone out.
            var affected = await db.SpeakerMeetingRequests
                .Where(r => r.Id == req.Id
                    && r.Status == MeetingRequestStatus.AwaitingSpeaker)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Status, MeetingRequestStatus.Pending)
                    .SetProperty(r => r.HallId, (Guid?)null)
                    .SetProperty(r => r.MeetingTableId, (Guid?)null)
                    .SetProperty(r => r.SlotStart, (DateTime?)null)
                    .SetProperty(r => r.SlotEnd, (DateTime?)null)
                    .SetProperty(r => r.AvailabilityWindowId, (Guid?)null)
                    .SetProperty(r => r.SpeakerDecisionAt, (DateTime?)null)
                    .SetProperty(r => r.RespondedAt, (DateTime?)null)
                    .SetProperty(r => r.RespondedByUserId, (Guid?)null)
                    .SetProperty(r => r.ResponseNote, (string?)null),
                    cancellationToken);
            if (affected == 0)
            {
                continue; // confirmed or re-decided under us; leave it be
            }
            reverted.Add(req);

            await auditLog.WriteSuccessAsync(
                AuditEvents.SpeakerMeetingRequestReverted,
                Guid.Empty,
                $"requestId={req.Id}; reason=AwaitingSpeakerTokensExpired",
                cancellationToken);
        }

        // The revert used to be completely silent: the requester's proposed
        // time was released and nobody was told. Tell them the speaker did not confirm
        // in time and the request is back with the team. Dispatched AFTER the write so a
        // notify failure can never undo the revert; best-effort (swallow-and-log).
        // Only the requester is notified — for the admin, the CP status flip back
        // into the Pending queue IS the signal.
        foreach (var req in reverted)
        {
            await NotifyRequesterRevertedAsync(db, notifications, logger, req, cancellationToken);
        }

        return reverted.Count;
    }

    /// <summary>The three fields the revert needs after the row itself has been
    /// updated out from under the scan: which request, who to tell, and whose name
    /// to put in the message.</summary>
    private readonly record struct StaleRequest(
        Guid Id, Guid RequestedByUserId, Guid SpeakerId);

    private static async Task NotifyRequesterRevertedAsync(
        SimfAppDbContext db, INotificationDispatcher notifications, ILogger logger,
        StaleRequest req, CancellationToken cancellationToken)
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
    /// The delegation twin of <see cref="RunExpiryScanAsync"/>. An AwaitingSpeaker
    /// DELEGATION meeting HOLDS its hall slot (<c>MeetingRequestStatuses.SlotHolding</c>)
    /// but nothing ever expired one, so a target delegation that simply never confirmed
    /// held the slot forever and the admin queue dead-ended. Same cadence, same shape and
    /// the same "no still-usable token" rule as the speaker sweep — the delegation
    /// confirm token carries the same 72h TTL, so this is time-based via that TTL. Reverts
    /// to a clean Pending (which releases the slot) so the admin can re-decide. Extracted
    /// for direct unit testing; returns the number actually reverted — a request a
    /// delegation member confirmed under the scan is left alone.
    /// </summary>
    internal static async Task<int> RunDelegationExpiryScanAsync(
        SimfAppDbContext db, IAuditLog auditLog, DateTime now,
        CancellationToken cancellationToken)
    {
        // AsNoTracking + id projection: the revert below is a conditional UPDATE.
        var staleIds = await db.DelegationMeetingRequests
            .AsNoTracking()
            .Where(r => r.Status == MeetingRequestStatus.AwaitingSpeaker
                && !db.DelegationMeetingActionTokens.Any(
                    t => t.DelegationMeetingRequestId == r.Id
                        && t.UsedAt == null && t.ExpiresAt > now))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        if (staleIds.Count == 0)
        {
            return 0;
        }

        var reverted = 0;
        foreach (var requestId in staleIds)
        {
            // Guarded on Status, like the speaker sweep above: ConfirmByOtherPartyAsync
            // accepts an in-app tap for as long as the row is AwaitingSpeaker, with no
            // 72h bound of its own, so an unguarded UPDATE here could revert a meeting
            // a member had just confirmed. ConfirmedAt / ConfirmedByUserId are cleared
            // with the rest of the response state — a Pending row that still carried a
            // confirmation stamp would read as accepted in the CP.
            var affected = await db.DelegationMeetingRequests
                .Where(r => r.Id == requestId
                    && r.Status == MeetingRequestStatus.AwaitingSpeaker)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Status, MeetingRequestStatus.Pending)
                    .SetProperty(r => r.HallId, (Guid?)null)
                    .SetProperty(r => r.MeetingTableId, (Guid?)null)
                    .SetProperty(r => r.SlotStart, (DateTime?)null)
                    .SetProperty(r => r.SlotEnd, (DateTime?)null)
                    .SetProperty(r => r.ConfirmedAt, (DateTime?)null)
                    .SetProperty(r => r.ConfirmedByUserId, (Guid?)null)
                    .SetProperty(r => r.RespondedAt, (DateTime?)null)
                    .SetProperty(r => r.RespondedByUserId, (Guid?)null)
                    .SetProperty(r => r.ResponseNote, (string?)null),
                    cancellationToken);
            if (affected == 0)
            {
                continue; // confirmed under us; leave it be
            }
            reverted++;

            await auditLog.WriteSuccessAsync(
                AuditEvents.DelegationMeetingRequestReverted,
                Guid.Empty,
                $"requestId={requestId}; reason=AwaitingConfirmTokenExpired",
                cancellationToken);
        }

        return reverted;
    }
}
