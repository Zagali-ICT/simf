// Tests: SIMF.Api.Tests/SessionNotAttendedReminderWorkerTests.cs
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
/// FR-903 (register defect <c>FR-903-not-attended-reminder</c>) — "the session
/// started but you have not attended". The booking half of FR-903 shipped
/// (BookingConfirmed / SessionReminder / BookingReleased); the not-attended half
/// had no kind and no sender. <see cref="ReservationNoShowReleaseWorker"/> — the
/// only worker that reasons about no-shows — frees the seat and notifies nobody.
///
/// <para>Once per minute this finds sessions that started at least
/// <see cref="ArrivalGrace"/> ago and are still inside
/// <see cref="ReminderWindow"/>, then nudges every holder of an ACTIVE seat
/// reservation who has NO <c>HallAttendance</c> row for that session — i.e. the
/// people who booked and have not arrived, while there is still a session to
/// arrive at.</para>
///
/// <para><b>Dedup.</b> Unlike D-217's <c>Session.ReminderSent</c> this needs
/// once-per-(attendee, session), not once-per-session: two holders of the same
/// session must both be nudged, and the sweep re-runs every tick inside the
/// window. That is exactly what the D-713 dispatcher guard gives —
/// <see cref="NotificationRequest.DeduplicateByRelatedEntity"/> with the session
/// as the related entity — so this worker needs NO new column and no claim/commit
/// dance: it is idempotent by construction, and a restart mid-sweep re-runs
/// harmlessly. The window bounds the repeated scanning.</para>
/// </summary>
internal sealed class SessionNotAttendedReminderWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IWorkerHeartbeatRegistry heartbeat,
    ILogger<SessionNotAttendedReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    // First tick is delayed so the host finishes migrations + seeding before the
    // worker hits the DB — mirrors SessionReminderWorker.
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    /// <summary>How long after <c>Start</c> a booked attendee is given to arrive
    /// before the nudge fires (the "+N minutes" of FR-903).</summary>
    internal static readonly TimeSpan ArrivalGrace = TimeSpan.FromMinutes(10);

    /// <summary>How long past <see cref="ArrivalGrace"/> the sweep keeps looking.
    /// Bounds the repeated scanning and stops a nudge arriving so late that the
    /// session is nearly over.</summary>
    internal static readonly TimeSpan ReminderWindow = TimeSpan.FromMinutes(20);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "SessionNotAttendedReminderWorker started (first poll in {Delay}s, then every {Interval}s;"
            + " grace {Grace}m, window {Window}m).",
            (int)StartupDelay.TotalSeconds, (int)PollInterval.TotalSeconds,
            (int)ArrivalGrace.TotalMinutes, (int)ReminderWindow.TotalMinutes);

        heartbeat.Register(
            nameof(SessionNotAttendedReminderWorker),
            "Nudges booked attendees who have not arrived after a session starts.",
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
                heartbeat.RecordSuccess(nameof(SessionNotAttendedReminderWorker));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                heartbeat.RecordFailure(nameof(SessionNotAttendedReminderWorker), ex.Message);
                logger.LogError(ex, "SessionNotAttendedReminderWorker tick failed.");
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

        var nudged = await RunNotAttendedScanAsync(
            db, notifications, timeProvider.GetUtcNow(), ArrivalGrace, ReminderWindow,
            logger, cancellationToken);
        if (nudged > 0)
        {
            logger.LogInformation(
                "SessionNotAttendedReminderWorker nudged {Count} attendee(s).", nudged);
        }
    }

    /// <summary>
    /// The core scan, extracted for direct unit testing. Returns the number of
    /// attendees nudged. A single attendee's dispatch failure is logged and
    /// skipped — it never aborts the batch.
    /// </summary>
    internal static async Task<int> RunNotAttendedScanAsync(
        SimfAppDbContext db, INotificationDispatcher notifications,
        DateTimeOffset now, TimeSpan arrivalGrace, TimeSpan reminderWindow,
        ILogger logger, CancellationToken cancellationToken)
    {
        // Started at least `grace` ago, and no longer ago than grace + window. The
        // session must also still be running — nudging someone to attend a finished
        // session is noise, not a reminder.
        var startedBefore = now - arrivalGrace;
        var startedAfter = now - arrivalGrace - reminderWindow;

        var due = await db.Sessions
            .AsNoTracking()
            .Where(s => s.IsActive
                && s.Start <= startedBefore
                && s.Start > startedAfter
                && s.End > now)
            .Select(s => new { s.Id, s.Title, s.TitleArabic })
            .ToListAsync(cancellationToken);
        if (due.Count == 0)
        {
            return 0;
        }

        var nudged = 0;
        foreach (var session in due)
        {
            // Booked (active reservation) MINUS arrived (any HallAttendance row for
            // the session — an open or a closed one; someone who arrived and left
            // did attend). Both tables are on SIMF_App, so this is one anti-join,
            // not a cross-database read.
            var absenteeIds = await db.SeatReservations
                .AsNoTracking()
                .Where(r => r.SessionId == session.Id
                    && r.ReleasedAt == null
                    && r.ReservedForUserId != null
                    && !db.HallAttendances.Any(a =>
                        a.SessionId == session.Id && a.UserId == r.ReservedForUserId))
                .Select(r => r.ReservedForUserId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var userId in absenteeIds)
            {
                try
                {
                    await notifications.DispatchAsync(new NotificationRequest
                    {
                        UserId = userId,
                        Kind = NotificationKind.SessionNotAttended,
                        Title = "The session has started",
                        TitleArabic = "بدأت الجلسة",
                        Body = $"\"{session.Title}\" has started and you have not arrived yet. "
                            + "Your seat is held for a short while longer.",
                        BodyArabic = $"بدأت جلسة \"{session.TitleArabic}\" ولم تصل بعد. "
                            + "سيبقى مقعدك محجوزاً لفترة قصيرة.",
                        Severity = NotificationSeverity.Warning,
                        RelatedEntityType = "Session",
                        RelatedEntityId = session.Id,
                        // Once per (attendee, session) — see the class remarks.
                        DeduplicateByRelatedEntity = true,
                        SendEmail = false,
                    }, cancellationToken);
                    nudged++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "SessionNotAttended dispatch failed for user {UserId} on session {SessionId}",
                        userId, session.Id);
                }
            }
        }

        return nudged;
    }
}
