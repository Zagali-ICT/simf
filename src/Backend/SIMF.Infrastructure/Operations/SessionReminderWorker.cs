// Tests: SIMF.Api.Tests/SessionReminderWorkerTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMF.Application.Notifications;
using SIMF.Application.Operations;
using SIMF.Common.Enums;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

namespace SIMF.Infrastructure.Operations;

/// <summary>
/// Background worker that fires the "session starting soon"
/// reminder. Once per minute it finds active sessions whose
/// <c>Start</c> falls inside the lead window and that have not yet been
/// reminded, then dispatches an in-app <see cref="NotificationKind.SessionReminder"/>
/// to every attendee with an active seat in that session.
///
/// <para>Dedup: <c>Session.ReminderSentAt</c> is the once-only guard.
/// A session is stamped and committed BEFORE its batch
/// is dispatched, so a restart mid-tick cannot resend (unlike an in-memory
/// set, or a stamp saved only after the whole loop). The notification rows
/// land on SIMF_Identity and cannot share a transaction with this SIMF_App
/// stamp, so claiming first makes a reminder at-most-once (a crash
/// may drop the rest of one session's batch) rather than re-sending it on
/// the next tick. Granularity is per-session: a visitor who books AFTER the
/// reminder fired does not get a late reminder — acceptable for a "starts in
/// {lead}" nudge.</para>
/// </summary>
internal sealed class SessionReminderWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IWorkerHeartbeatRegistry heartbeat,
    ILogger<SessionReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    // First tick is delayed so the host finishes migrations + seeding before
    // the worker hits the DB — mirrors RegistrationGateAutoCloseWorker.
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    /// <summary>How far ahead of <c>Start</c> the reminder fires. A session
    /// is reminded once, the first poll after it enters this window.</summary>
    internal static readonly TimeSpan ReminderLeadTime = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "SessionReminderWorker started (first poll in {Delay}s, then every {Interval}s; lead {Lead}m).",
            (int)StartupDelay.TotalSeconds, (int)PollInterval.TotalSeconds,
            (int)ReminderLeadTime.TotalMinutes);

        heartbeat.Register(
            nameof(SessionReminderWorker),
            "Sends 'session starting soon' reminders to booked attendees.",
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
                heartbeat.RecordSuccess(nameof(SessionReminderWorker));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                heartbeat.RecordFailure(nameof(SessionReminderWorker), ex.Message);
                logger.LogError(ex, "SessionReminderWorker tick failed.");
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
            db, notifications, timeProvider.SimfNow(), ReminderLeadTime, logger,
            cancellationToken);
        if (reminded > 0)
        {
            logger.LogInformation(
                "SessionReminderWorker reminded {Count} session(s).", reminded);
        }
    }

    /// <summary>
    /// The core scan, extracted for direct unit testing. For each due session it
    /// claims the session — one conditional UPDATE stamping <c>ReminderSentAt</c>
    /// while it is still null, committed BEFORE dispatch — then notifies every
    /// attendee with an active seat, so neither a restart mid-tick nor a second
    /// worker instance can re-send. Returns the number of sessions actually
    /// claimed, which is not the number found due: a session another instance
    /// claimed first is skipped and not counted. A single attendee's dispatch
    /// failure is logged and skipped — it never aborts the batch.
    /// </summary>
    internal static async Task<int> RunReminderScanAsync(
        SimfAppDbContext db, INotificationDispatcher notifications,
        DateTime now, TimeSpan leadTime, ILogger logger,
        CancellationToken cancellationToken)
    {
        var windowEnd = now + leadTime;
        // AsNoTracking: the claim below is a conditional UPDATE, so nothing loaded
        // here may stay tracked and be re-saved by a later SaveChanges.
        var due = await db.Sessions
            .AsNoTracking()
            .Where(s => s.IsActive
                && s.ReminderSentAt == null
                && s.Start > now
                && s.Start <= windowEnd)
            .Select(s => new { s.Id, s.Title, s.TitleArabic })
            .ToListAsync(cancellationToken);
        if (due.Count == 0)
        {
            return 0;
        }

        var reminded = 0;
        foreach (var session in due)
        {
            // Claim the session BEFORE dispatching: stamp ReminderSentAt in one
            // conditional UPDATE that only lands while it is still unstamped, so a
            // restart mid-batch (or a second worker instance) cannot re-send this
            // session's reminder. The tracked read-then-write this replaces was
            // last-writer-wins, not a claim: with two instances polling at once both
            // read null, both stamped, and every attendee of the session got the
            // reminder twice. A claim affecting 0 rows means the other instance won.
            // The notification writes land on SIMF_Identity and cannot share a
            // transaction with this SIMF_App stamp. A zero-attendee session is still
            // claimed so the worker stops re-scanning it every minute until it starts.
            var claimed = await db.Sessions
                .Where(s => s.Id == session.Id && s.ReminderSentAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(s => s.ReminderSentAt, now),
                    cancellationToken);
            if (claimed == 0)
            {
                continue;
            }
            reminded++;

            // A seat is held by an attendee PROFILE; the reminder is delivered to
            // the ACCOUNT behind it, so join through UserProfile (same database).
            // A holder with no account is skipped — there is no device to remind.
            var attendeeIds = await db.SeatReservations
                .Where(r => r.SessionId == session.Id
                    && r.ReleasedAt == null
                    && r.ReservedForProfileId != null)
                .Join(db.UserProfiles,
                    r => r.ReservedForProfileId!.Value,
                    p => p.Id,
                    (r, p) => p.UserId)
                .Where(userId => userId != null)
                .Select(userId => userId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var userId in attendeeIds)
            {
                try
                {
                    await notifications.DispatchAsync(new NotificationRequest
                    {
                        UserId = userId,
                        Kind = NotificationKind.SessionReminder,
                        Title = "Session starting soon",
                        TitleArabic = "تبدأ الجلسة قريباً",
                        Body = $"\"{session.Title}\" is starting soon. See you there.",
                        BodyArabic = $"تبدأ جلسة \"{session.TitleArabic}\" قريباً. نراك هناك.",
                        Severity = NotificationSeverity.Info,
                        RelatedEntityType = "Session",
                        RelatedEntityId = session.Id,
                        SendEmail = false,
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "SessionReminder dispatch failed for user {UserId} on session {SessionId}",
                        userId, session.Id);
                }
            }
        }

        return reminded;
    }
}
