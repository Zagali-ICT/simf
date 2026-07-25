// Tests: SIMF.Api.Tests/SessionReminderWorkerTests.cs
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
/// P1.7 (D-217) — background worker that fires the "session starting soon"
/// reminder. Once per minute it finds active sessions whose
/// <c>Start</c> falls inside the lead window and that have not yet been
/// reminded, then dispatches an in-app <see cref="NotificationKind.SessionReminder"/>
/// to every attendee with an active seat in that session.
///
/// <para>Dedup: <c>Session.ReminderSent</c> is the once-only guard
/// (D-217 freeze-lift). A session is stamped and committed BEFORE its batch
/// is dispatched, so a restart mid-tick cannot resend (unlike an in-memory
/// set, or a stamp saved only after the whole loop). The notification rows
/// land on SIMF_Identity and cannot share a transaction with this SIMF_App
/// stamp (D-157), so claiming first makes a reminder at-most-once (a crash
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
            db, notifications, timeProvider.GetUtcNow(), ReminderLeadTime, logger,
            cancellationToken);
        if (reminded > 0)
        {
            logger.LogInformation(
                "SessionReminderWorker reminded {Count} session(s).", reminded);
        }
    }

    /// <summary>
    /// The core scan, extracted for direct unit testing. For each due session
    /// it claims the session — stamping <c>ReminderSent</c> and committing
    /// it BEFORE dispatch — then notifies every attendee with an active seat,
    /// so a restart mid-tick can never re-send. Returns the number of sessions
    /// reminded. A single attendee's dispatch failure is logged and skipped —
    /// it never aborts the batch.
    /// </summary>
    internal static async Task<int> RunReminderScanAsync(
        SimfAppDbContext db, INotificationDispatcher notifications,
        DateTimeOffset now, TimeSpan leadTime, ILogger logger,
        CancellationToken cancellationToken)
    {
        var windowEnd = now + leadTime;
        var due = await db.Sessions
            .Where(s => s.IsActive
                && s.ReminderSent == null
                && s.Start > now
                && s.Start <= windowEnd)
            .ToListAsync(cancellationToken);
        if (due.Count == 0)
        {
            return 0;
        }

        foreach (var session in due)
        {
            var attendeeIds = await db.SeatReservations
                .Where(r => r.SessionId == session.Id
                    && r.ReleasedAt == null
                    && r.ReservedForUserId != null)
                .Select(r => r.ReservedForUserId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Claim the session BEFORE dispatching: stamp ReminderSent and
            // commit it so a restart mid-batch (or a second worker instance)
            // cannot re-send this session's reminder. The notification writes
            // land on SIMF_Identity and cannot share a transaction with this
            // SIMF_App stamp (D-157). A zero-attendee session is still claimed
            // so the worker stops re-scanning it every minute until it starts.
            session.ReminderSent = now;
            await db.SaveChangesAsync(cancellationToken);

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

        return due.Count;
    }
}
