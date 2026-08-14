// Tests: SIMF.Api.Tests/SessionRatingPromptWorkerTests.cs
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
/// Background worker that fires the "please rate this session" prompt. Once per
/// minute it finds active sessions that have just ended (<c>End</c> within a
/// recent back-fill window) and have not yet been prompted, then dispatches an
/// in-app <see cref="NotificationKind.SessionRatingRequest"/> to every attendee
/// who checked in to the hall for that session (a <c>HallAttendance</c> row) —
/// not everyone who merely booked a seat. The notification's
/// <c>RelatedEntityType="Session"</c> + <c>RelatedEntityId</c> let the app
/// deep-link to the session's rating screen.
///
/// <para>Dedup: <c>Session.RatingPromptSent</c> is the once-only guard. A
/// session is stamped after its batch is dispatched, so a restart cannot resend.
/// The back-fill window also bounds the first run after deploy so it never blasts
/// every historical session.</para>
/// </summary>
internal sealed class SessionRatingPromptWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IWorkerHeartbeatRegistry heartbeat,
    ILogger<SessionRatingPromptWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    // First tick is delayed so the host finishes migrations + seeding first
    // (mirrors SessionReminderWorker).
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    /// <summary>How far back from now an ended session is still eligible for a
    /// prompt. Bounds the first run after a deploy so already-ended sessions are
    /// not all prompted at once; a session is prompted once, the first poll after
    /// it ends.</summary>
    internal static readonly TimeSpan BackfillWindow = TimeSpan.FromHours(6);

    /// <summary>The CP rating-type <c>Code</c> whose active state (RatingConfig)
    /// gates this prompt: deactivating the "Session" type in the CP silences the
    /// end-of-session rating notification.</summary>
    private const string SessionRatingTypeCode = "Session";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "SessionRatingPromptWorker started (first poll in {Delay}s, then every {Interval}s; window {Window}h).",
            (int)StartupDelay.TotalSeconds, (int)PollInterval.TotalSeconds,
            (int)BackfillWindow.TotalHours);

        heartbeat.Register(
            nameof(SessionRatingPromptWorker),
            "Prompts attendees to rate a session after it ends.",
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
                heartbeat.RecordSuccess(nameof(SessionRatingPromptWorker));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                heartbeat.RecordFailure(nameof(SessionRatingPromptWorker), ex.Message);
                logger.LogError(ex, "SessionRatingPromptWorker tick failed.");
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

        var prompted = await RunRatingPromptScanAsync(
            db, notifications, timeProvider.SimfNow(), BackfillWindow, logger, cancellationToken);
        if (prompted > 0)
        {
            logger.LogInformation(
                "SessionRatingPromptWorker prompted {Count} session(s).", prompted);
        }
    }

    /// <summary>
    /// The core scan, extracted for direct unit testing. Notifies every attendee
    /// who checked in to the hall for each just-ended session, then stamps
    /// <c>RatingPromptSent</c> so each session is prompted exactly once.
    /// Returns the number of sessions prompted. A single attendee's dispatch
    /// failure is logged and skipped — it never aborts the batch or blocks the
    /// dedup stamp.
    /// </summary>
    internal static async Task<int> RunRatingPromptScanAsync(
        SimfAppDbContext db, INotificationDispatcher notifications,
        DateTime now, TimeSpan backfillWindow, ILogger logger,
        CancellationToken cancellationToken)
    {
        // Respect the CP: if an admin deactivated the "Session" rating type in
        // RatingConfig, send no session prompt (the rate form already 404s for an
        // inactive type, so a prompt would deep-link to an unavailable form). Not
        // stamped, so re-enabling resumes prompts for sessions still in the window.
        var sessionRatingEnabled = await db.RatingTypes
            .AnyAsync(t => t.Code == SessionRatingTypeCode && t.IsActive, cancellationToken);
        if (!sessionRatingEnabled)
        {
            return 0;
        }

        var windowStart = now - backfillWindow;
        var due = await db.Sessions
            .Where(s => s.IsActive
                && s.RatingPromptSent == null
                && s.End <= now
                && s.End >= windowStart)
            .ToListAsync(cancellationToken);
        if (due.Count == 0)
        {
            return 0;
        }

        foreach (var session in due)
        {
            // Audience = attendees who actually CHECKED IN to the hall for this
            // session (a HallAttendance row), NOT everyone holding a seat booking.
            // This mirrors the submit gate (RatingFormService.IsAttendedAsync,
            // PerSession), so a prompted user can always submit, and it stops a
            // booked-but-absent user being asked to rate a session they never
            // attended. It also covers attendees who never scanned out — the
            // HallAttendanceCloseoutWorker auto-closes their row without dispatching
            // the departure prompt, but their check-in row already exists here.
            //
            // Attendance is keyed by the attendee PROFILE; the prompt is delivered
            // to the ACCOUNT behind it, so join through UserProfile (same database).
            // A walk-in with no account attended and is simply not prompted — the
            // rating form is in the app they do not have.
            var attendeeIds = await db.HallAttendances
                .Where(a => a.SessionId == session.Id)
                .Join(db.UserProfiles,
                    a => a.UserProfileId,
                    p => p.Id,
                    (a, p) => p.UserId)
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
                        Kind = NotificationKind.SessionRatingRequest,
                        Title = "Rate this session",
                        TitleArabic = "قيّم هذه الجلسة",
                        Body = $"How was \"{session.Title}\"? Tap to rate it.",
                        BodyArabic = $"كيف كانت جلسة \"{session.TitleArabic}\"؟ اضغط للتقييم.",
                        Severity = NotificationSeverity.Info,
                        RelatedEntityType = "Session",
                        RelatedEntityId = session.Id,
                        SendEmail = false,
                        // Share the one-per-(session, user) guard with the
                        // hall-departure hook (GAP-A): if the attendee already got a
                        // rating prompt for this session when they left the hall,
                        // the clock-end worker does not send a second.
                        DeduplicateByRelatedEntity = true,
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "SessionRatingRequest dispatch failed for user {UserId} on session {SessionId}",
                        userId, session.Id);
                }
            }

            // Stamp even a zero-attendee session so the worker stops re-scanning it.
            session.RatingPromptSent = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return due.Count;
    }
}
