// Tests: SIMF.Api.Tests/SessionRatingPromptWorkerTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMF.Application.Notifications;
using SIMF.Common.Enums;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Operations;

/// <summary>
/// Background worker that fires the "please rate this session" prompt. Once per
/// minute it finds active sessions that have just ended (<c>EndUtc</c> within a
/// recent back-fill window) and have not yet been prompted, then dispatches an
/// in-app <see cref="NotificationKind.SessionRatingRequest"/> to every attendee
/// with an active seat in that session. The notification's
/// <c>RelatedEntityType="Session"</c> + <c>RelatedEntityId</c> let the app
/// deep-link to the session's rating screen.
///
/// <para>Dedup: <c>Session.RatingPromptSentUtc</c> is the once-only guard. A
/// session is stamped after its batch is dispatched, so a restart cannot resend.
/// The back-fill window also bounds the first run after deploy so it never blasts
/// every historical session.</para>
/// </summary>
internal sealed class SessionRatingPromptWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "SessionRatingPromptWorker started (first poll in {Delay}s, then every {Interval}s; window {Window}h).",
            (int)StartupDelay.TotalSeconds, (int)PollInterval.TotalSeconds,
            (int)BackfillWindow.TotalHours);

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
            db, notifications, timeProvider.GetUtcNow(), BackfillWindow, logger, cancellationToken);
        if (prompted > 0)
        {
            logger.LogInformation(
                "SessionRatingPromptWorker prompted {Count} session(s).", prompted);
        }
    }

    /// <summary>
    /// The core scan, extracted for direct unit testing. Notifies every attendee
    /// with an active seat in each just-ended session, then stamps
    /// <c>RatingPromptSentUtc</c> so each session is prompted exactly once.
    /// Returns the number of sessions prompted. A single attendee's dispatch
    /// failure is logged and skipped — it never aborts the batch or blocks the
    /// dedup stamp.
    /// </summary>
    internal static async Task<int> RunRatingPromptScanAsync(
        SimfAppDbContext db, INotificationDispatcher notifications,
        DateTimeOffset now, TimeSpan backfillWindow, ILogger logger,
        CancellationToken cancellationToken)
    {
        var windowStart = now - backfillWindow;
        var due = await db.Sessions
            .Where(s => s.IsActive
                && s.RatingPromptSentUtc == null
                && s.EndUtc <= now
                && s.EndUtc >= windowStart)
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
            session.RatingPromptSentUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return due.Count;
    }
}
