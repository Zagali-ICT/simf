// Tests: SIMF.Api.Tests/ProgrammeRatingPromptWorkerTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMF.Application.Notifications;
using SIMF.Application.Operations;
using SIMF.Common.Enums;
using SIMF.Domain.Configuration;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Operations;

/// <summary>
/// Background worker for the two coarse rating prompts above the per-session one
/// (D-679):
/// <list type="bullet">
/// <item><b>End of each programme day</b> — once a day has ended, dispatch a
/// <see cref="NotificationKind.DayRatingRequest"/> to every attendee who
/// <b>checked in that day</b> (a Check-In gate scan). Dedup guard:
/// <c>ProgrammeDay.RatingPromptSentUtc</c> (per-row, like the session worker).</item>
/// <item><b>End of the whole programme</b> — once the last day has ended,
/// dispatch the overall <see cref="NotificationKind.EventRatingRequest"/>,
/// <see cref="NotificationKind.ExhibitionRatingRequest"/> and
/// <see cref="NotificationKind.AppRatingRequest"/> to every attendee who ever
/// checked in. Dedup guard: a single <see cref="SystemSetting"/> row keyed
/// <see cref="ProgramEndSettingKey"/> (a global marker, not per-row).</item>
/// </list>
///
/// <para>The audience for both is derived from <c>GateScan</c> rows on the App DB:
/// a Check-In / Allowed scan whose <c>UserProfileId</c> resolves (via
/// <c>UserProfile.Id</c>) to <c>UserProfile.UserId</c> — the <c>SimfUser.Id</c>
/// that the notification is addressed to. No cross-DB join (both live on
/// <see cref="SimfAppDbContext"/>).</para>
///
/// <para>Day boundaries are event-local (UTC+3, the codebase
/// <c>EventTimeZoneOffset</c> convention): a session belongs to the day whose
/// local calendar date matches its <c>StartUtc</c>. A day "ends" at the latest
/// session end + <see cref="SessionGrace"/>, or (for a session-less day) at the
/// next local midnight. The back-fill windows bound the first run after a deploy
/// so an old day / programme is never blasted.</para>
/// </summary>
internal sealed class ProgrammeRatingPromptWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IWorkerHeartbeatRegistry heartbeat,
    ILogger<ProgrammeRatingPromptWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    // First tick is delayed so the host finishes migrations + seeding first.
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    /// <summary>Event-local offset (UTC+3) — the codebase convention for bucketing
    /// sessions into calendar days (mirrors <c>MyAreaService</c> /
    /// <c>ProgrammeSessionService</c>).</summary>
    internal static readonly TimeSpan EventOffset = TimeSpan.FromHours(3);

    /// <summary>Grace after the last session of a day before the day is treated as
    /// "ended" and the day prompt fires.</summary>
    internal static readonly TimeSpan SessionGrace = TimeSpan.FromMinutes(30);

    /// <summary>Extra grace after the final day ends before the whole-programme
    /// trio fires, so it never collides with that day's own day prompt.</summary>
    internal static readonly TimeSpan ProgramEndGrace = TimeSpan.FromHours(1);

    /// <summary>How far back an ended day / programme is still eligible, so the
    /// first run after a deploy never prompts long-past dates.</summary>
    internal static readonly TimeSpan BackfillWindow = TimeSpan.FromHours(24);

    /// <summary>The <see cref="SystemSetting"/> key holding the once-only marker
    /// for the end-of-programme trio.</summary>
    internal const string ProgramEndSettingKey = "Notifications:ProgramEndRatingSentUtc";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "ProgrammeRatingPromptWorker started (first poll in {Delay}s, then every {Interval}s; window {Window}h).",
            (int)StartupDelay.TotalSeconds, (int)PollInterval.TotalSeconds,
            (int)BackfillWindow.TotalHours);

        heartbeat.Register(
            nameof(ProgrammeRatingPromptWorker),
            "Sends end-of-day and end-of-programme rating prompts.",
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
                heartbeat.RecordSuccess(nameof(ProgrammeRatingPromptWorker));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                heartbeat.RecordFailure(nameof(ProgrammeRatingPromptWorker), ex.Message);
                logger.LogError(ex, "ProgrammeRatingPromptWorker tick failed.");
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
        var now = timeProvider.GetUtcNow();

        var days = await RunDayPromptScanAsync(db, notifications, now, BackfillWindow, logger, cancellationToken);
        if (days > 0)
        {
            logger.LogInformation("ProgrammeRatingPromptWorker prompted {Count} programme day(s).", days);
        }

        var closed = await RunProgramEndScanAsync(db, notifications, now, BackfillWindow, logger, cancellationToken);
        if (closed)
        {
            logger.LogInformation("ProgrammeRatingPromptWorker dispatched the end-of-programme rating trio.");
        }
    }

    /// <summary>
    /// End-of-day scan, extracted for direct unit testing. For every active day
    /// that has ended within the back-fill window and not yet been prompted, it
    /// claims the day — stamping <c>RatingPromptSentUtc</c> and committing it
    /// BEFORE dispatch (even a zero-recipient day, so it is not re-scanned) —
    /// then notifies each attendee who checked in that day, so a restart mid-loop
    /// cannot re-send an already-processed day. Returns the number of days prompted.
    /// </summary>
    internal static async Task<int> RunDayPromptScanAsync(
        SimfAppDbContext db, INotificationDispatcher notifications,
        DateTimeOffset now, TimeSpan backfillWindow, ILogger logger,
        CancellationToken cancellationToken)
    {
        // Respect the CP: if an admin deactivated the "Day" rating type in
        // RatingConfig, send no day prompt (not stamped, so re-enabling resumes).
        if (!await db.RatingTypes.AnyAsync(t => t.Code == "Day" && t.IsActive, cancellationToken))
        {
            return 0;
        }

        var days = await db.ProgrammeDays
            .Where(d => d.IsActive && d.RatingPromptSentUtc == null)
            .ToListAsync(cancellationToken);
        if (days.Count == 0)
        {
            return 0;
        }

        var sessions = await db.Sessions
            .Where(s => s.IsActive)
            .Select(s => new SessionWindow(s.StartUtc, s.EndUtc))
            .ToListAsync(cancellationToken);

        var windowStart = now - backfillWindow;
        var prompted = 0;

        foreach (var day in days)
        {
            var dayEnd = DayEndUtc(day.Date, sessions);
            if (dayEnd > now || dayEnd < windowStart)
            {
                continue; // not ended yet, or too far in the past to back-fill
            }

            var recipientIds = await CheckedInUserIdsAsync(db, day.Date, cancellationToken);

            // Claim the day BEFORE dispatching (see the class remarks): stamp
            // RatingPromptSentUtc and commit it so a restart mid-loop cannot
            // re-send an already-processed day. Even a zero-recipient day is
            // claimed so the worker stops re-scanning it.
            day.RatingPromptSentUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            prompted++;

            foreach (var userId in recipientIds)
            {
                try
                {
                    await notifications.DispatchAsync(new NotificationRequest
                    {
                        UserId = userId,
                        Kind = NotificationKind.DayRatingRequest,
                        Title = "Rate today's programme",
                        TitleArabic = "قيّم برنامج اليوم",
                        Body = $"How was \"{day.Title}\"? Tap to rate today.",
                        BodyArabic = $"كيف كان \"{day.TitleArabic}\"؟ اضغط لتقييم اليوم.",
                        Severity = NotificationSeverity.Info,
                        RelatedEntityType = "ProgrammeDay",
                        RelatedEntityId = day.Id,
                        SendEmail = false,
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "DayRatingRequest dispatch failed for user {UserId} on day {DayId}",
                        userId, day.Id);
                }
            }
        }

        return prompted;
    }

    /// <summary>
    /// End-of-programme scan, extracted for direct unit testing. Once the last
    /// active day has ended (+ <see cref="ProgramEndGrace"/>) within the back-fill
    /// window and the <see cref="ProgramEndSettingKey"/> marker is unset, it
    /// writes the once-only marker FIRST and then dispatches the Event +
    /// Exhibition + App rating requests to every attendee who ever checked in —
    /// so a restart mid-dispatch cannot re-fire the whole trio to the audience.
    /// Returns <c>true</c> when it fired.
    /// </summary>
    internal static async Task<bool> RunProgramEndScanAsync(
        SimfAppDbContext db, INotificationDispatcher notifications,
        DateTimeOffset now, TimeSpan backfillWindow, ILogger logger,
        CancellationToken cancellationToken)
    {
        var lastDay = await db.ProgrammeDays
            .Where(d => d.IsActive)
            .OrderByDescending(d => d.Date)
            .FirstOrDefaultAsync(cancellationToken);
        if (lastDay is null)
        {
            return false;
        }

        var sessions = await db.Sessions
            .Where(s => s.IsActive)
            .Select(s => new SessionWindow(s.StartUtc, s.EndUtc))
            .ToListAsync(cancellationToken);

        var programEnd = DayEndUtc(lastDay.Date, sessions) + ProgramEndGrace;
        if (programEnd > now || now - programEnd > backfillWindow)
        {
            return false; // not ended yet, or too old to back-fill on first deploy
        }

        if (await db.SystemSettings.AnyAsync(s => s.Key == ProgramEndSettingKey, cancellationToken))
        {
            return false; // already dispatched
        }

        // Respect the CP: only the overall rating types still active in RatingConfig
        // are prompted. If all three are deactivated, skip entirely (no marker) so
        // re-enabling one later still fires.
        var activeOverallCodes = await db.RatingTypes
            .Where(t => t.IsActive
                && (t.Code == "Event" || t.Code == "Exhibition" || t.Code == "App"))
            .Select(t => t.Code)
            .ToListAsync(cancellationToken);
        if (activeOverallCodes.Count == 0)
        {
            return false;
        }

        var recipientIds = await CheckedInUserIdsAsync(db, day: null, cancellationToken);

        // Claim the once-only marker BEFORE dispatching the trio so a restart
        // mid-dispatch cannot re-fire it to the whole audience. The notification
        // rows land on SIMF_Identity and cannot share a transaction with this
        // SIMF_App marker (D-157), so committing the marker first makes the trio
        // at-most-once (a crash may drop the rest) rather than re-blasting every
        // checked-in attendee on the next tick. Kept inactive so it does not
        // surface in the admin System Settings list; the dedup check ignores
        // IsActive.
        db.SystemSettings.Add(new SystemSetting
        {
            Id = Guid.NewGuid(),
            Key = ProgramEndSettingKey,
            Value = now.ToString("O"),
            Description = "Internal marker: end-of-programme rating prompts dispatched (D-679).",
            IsActive = false,
            CreatedBy = Guid.Empty,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);

        var trio = new (NotificationKind Kind, string Code, string Title, string TitleAr, string Body, string BodyAr)[]
        {
            (NotificationKind.EventRatingRequest, "Event", "Rate the forum", "قيّم الملتقى",
                "The forum has ended — tap to share your overall rating.",
                "انتهى الملتقى — اضغط لمشاركة تقييمك العام."),
            (NotificationKind.ExhibitionRatingRequest, "Exhibition", "Rate the exhibition", "قيّم المعرض",
                "Tap to rate the exhibition.", "اضغط لتقييم المعرض."),
            (NotificationKind.AppRatingRequest, "App", "Rate the app", "قيّم التطبيق",
                "Enjoying the app? Tap to rate it.", "هل أعجبك التطبيق؟ اضغط لتقييمه."),
        };

        foreach (var userId in recipientIds)
        {
            foreach (var (kind, code, title, titleAr, body, bodyAr) in trio)
            {
                if (!activeOverallCodes.Contains(code))
                {
                    continue; // this overall rating is deactivated in the CP
                }
                try
                {
                    await notifications.DispatchAsync(new NotificationRequest
                    {
                        UserId = userId,
                        Kind = kind,
                        Title = title,
                        TitleArabic = titleAr,
                        Body = body,
                        BodyArabic = bodyAr,
                        Severity = NotificationSeverity.Info,
                        SendEmail = false,
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "{Kind} dispatch failed for user {UserId} at programme end", kind, userId);
                }
            }
        }

        return true;
    }

    /// <summary>Distinct <c>SimfUser.Id</c>s of attendees with an Allowed Check-In
    /// gate scan — for the whole programme when <paramref name="day"/> is null, or
    /// within that event-local day otherwise. Resolves the two-hop
    /// GateScan.UserProfileId → UserProfile.Id → UserProfile.UserId (the
    /// notification recipient id-space).</summary>
    private static async Task<List<Guid>> CheckedInUserIdsAsync(
        SimfAppDbContext db, DateOnly? day, CancellationToken cancellationToken)
    {
        var scans = db.GateScans.Where(g =>
            g.Direction == ScanDirection.CheckIn
            && g.Outcome == ScanOutcome.Allowed
            && g.UserProfileId != null);

        if (day is { } d)
        {
            var dayStart = new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), EventOffset);
            var dayEndBoundary = dayStart.AddDays(1);
            scans = scans.Where(g => g.ScannedAtUtc >= dayStart && g.ScannedAtUtc < dayEndBoundary);
        }

        var profileIds = await scans
            .Select(g => g.UserProfileId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (profileIds.Count == 0)
        {
            return [];
        }

        return await db.UserProfiles
            .Where(p => profileIds.Contains(p.Id))
            .Select(p => p.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    /// <summary>When an event-local <paramref name="date"/> "ends": the latest end
    /// of its active sessions + <see cref="SessionGrace"/>, or the next local
    /// midnight if the day has no sessions.</summary>
    internal static DateTimeOffset DayEndUtc(DateOnly date, IReadOnlyCollection<SessionWindow> sessions)
    {
        var daySessions = sessions
            .Where(s => DateOnly.FromDateTime(s.StartUtc.ToOffset(EventOffset).DateTime) == date)
            .ToList();
        if (daySessions.Count > 0)
        {
            return daySessions.Max(s => s.EndUtc) + SessionGrace;
        }
        return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), EventOffset).AddDays(1);
    }

    /// <summary>A session's UTC start/end, projected for in-memory day bucketing.</summary>
    internal readonly record struct SessionWindow(DateTimeOffset StartUtc, DateTimeOffset EndUtc);
}
