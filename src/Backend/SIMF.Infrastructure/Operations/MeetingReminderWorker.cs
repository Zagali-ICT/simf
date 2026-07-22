// Tests: SIMF.Api.Tests/MeetingReminderWorkerTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMF.Application.Notifications;
using SIMF.Application.Operations;
using SIMF.Common.Enums;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Operations;

/// <summary>
/// Bi-Meeting rework — background worker that fires the "meeting starting soon"
/// reminder 15 minutes before a CONFIRMED (<see cref="MeetingRequestStatus.Accepted"/>)
/// bilateral meeting (speaker or delegation) with a bound slot. Mirrors
/// <see cref="SessionReminderWorker"/>: once per minute it finds confirmed meetings whose
/// <c>SlotStartUtc</c> falls inside the lead window and that have not yet been reminded,
/// stamps <c>ReminderSentUtc</c> and commits it BEFORE dispatching (at-most-once dedup,
/// per D-157 the notification rows land on SIMF_Identity and cannot share a transaction
/// with this SIMF_App stamp), then dispatches an in-app + email
/// <see cref="NotificationKind.MeetingReminder"/> to the parties.
/// </summary>
internal sealed class MeetingReminderWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IWorkerHeartbeatRegistry heartbeat,
    ILogger<MeetingReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    /// <summary>How far ahead of <c>SlotStartUtc</c> the reminder fires (owner: 15 min).</summary>
    internal static readonly TimeSpan ReminderLeadTime = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "MeetingReminderWorker started (first poll in {Delay}s, then every {Interval}s; lead {Lead}m).",
            (int)StartupDelay.TotalSeconds, (int)PollInterval.TotalSeconds,
            (int)ReminderLeadTime.TotalMinutes);

        heartbeat.Register(
            nameof(MeetingReminderWorker),
            "Sends 'meeting starting soon' reminders (email + app) 15 minutes before a confirmed meeting.",
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
                heartbeat.RecordSuccess(nameof(MeetingReminderWorker));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                heartbeat.RecordFailure(nameof(MeetingReminderWorker), ex.Message);
                logger.LogError(ex, "MeetingReminderWorker tick failed.");
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
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        var reminded = await RunReminderScanAsync(
            db, notifications, timeProvider.GetUtcNow(), ReminderLeadTime, logger,
            cancellationToken);
        if (reminded > 0)
        {
            logger.LogInformation("MeetingReminderWorker reminded {Count} meeting(s).", reminded);
        }
    }

    /// <summary>The core scan, extracted for direct unit testing. Claims each due meeting
    /// (stamp + commit ReminderSentUtc) BEFORE dispatch, then notifies the parties. Returns
    /// the number of meetings reminded.</summary>
    internal static async Task<int> RunReminderScanAsync(
        SimfAppDbContext db, INotificationDispatcher notifications,
        DateTimeOffset now, TimeSpan leadTime, ILogger logger,
        CancellationToken cancellationToken)
    {
        var windowEnd = now + leadTime;
        var count = 0;

        // Speaker meetings — confirmed, slot inside the lead window, not yet reminded.
        var speakerDue = await db.SpeakerMeetingRequests.AsNoTracking()
            .Where(r => r.Status == MeetingRequestStatus.Accepted
                && r.ReminderSentUtc == null
                && r.SlotStartUtc != null
                && r.SlotStartUtc > now && r.SlotStartUtc <= windowEnd)
            .ToListAsync(cancellationToken);
        foreach (var req in speakerDue)
        {
            // Claim conditionally so a meeting cancelled / checked-in AFTER the batch load
            // is not reminded: stamp ReminderSentUtc only while the row is still Accepted +
            // unreminded, and dispatch only when this claim actually won (affected == 1) —
            // at-most-once, no stale "starts soon" for a now-cancelled meeting. Mirrors the
            // conditional-update claim in ConfirmByOtherPartyAsync / MeetingActionToken.
            var claimed = await db.SpeakerMeetingRequests
                .Where(r => r.Id == req.Id
                    && r.Status == MeetingRequestStatus.Accepted
                    && r.ReminderSentUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ReminderSentUtc, now), cancellationToken);
            if (claimed == 0) { continue; }
            await DispatchAsync(notifications, req.RequestedByUserId, req.Subject, req.Id,
                "SpeakerMeetingRequest", logger, cancellationToken);
            count++;
        }

        // Delegation meetings — confirmed, slot inside the lead window, not yet reminded.
        var delegationDue = await db.DelegationMeetingRequests.AsNoTracking()
            .Where(r => r.Status == MeetingRequestStatus.Accepted
                && r.ReminderSentUtc == null
                && r.SlotStartUtc != null
                && r.SlotStartUtc > now && r.SlotStartUtc <= windowEnd)
            .ToListAsync(cancellationToken);
        foreach (var req in delegationDue)
        {
            // Same conditional claim as the speaker loop (at-most-once, cancel-safe).
            var claimed = await db.DelegationMeetingRequests
                .Where(r => r.Id == req.Id
                    && r.Status == MeetingRequestStatus.Accepted
                    && r.ReminderSentUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ReminderSentUtc, now), cancellationToken);
            if (claimed == 0) { continue; }

            // Both parties: the requester + every eligible member of the target delegation.
            var recipientIds = await db.UserProfiles.AsNoTracking()
                .Where(p => p.NationalityId == req.TargetCountryId && p.AllowsDelegationMeeting)
                .Select(p => p.UserId)
                .ToListAsync(cancellationToken);
            recipientIds.Add(req.RequestedByUserId);
            foreach (var userId in recipientIds.Distinct())
            {
                await DispatchAsync(notifications, userId, req.Subject, req.Id,
                    "DelegationMeetingRequest", logger, cancellationToken);
            }
            count++;
        }

        return count;
    }

    private static async Task DispatchAsync(
        INotificationDispatcher notifications, Guid userId, string subject, Guid meetingId,
        string relatedType, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await notifications.DispatchAsync(new NotificationRequest
            {
                UserId = userId,
                Kind = NotificationKind.MeetingReminder,
                Title = "Meeting starting soon",
                TitleArabic = "يبدأ الاجتماع قريباً",
                Body = $"Your meeting \"{subject}\" starts in about 15 minutes.",
                BodyArabic = $"يبدأ اجتماعك \"{subject}\" خلال 15 دقيقة تقريباً.",
                Severity = NotificationSeverity.Info,
                RelatedEntityType = relatedType,
                RelatedEntityId = meetingId,
                SendEmail = true,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "MeetingReminder dispatch failed for user {UserId} on meeting {MeetingId}",
                userId, meetingId);
        }
    }
}
