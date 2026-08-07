// Tests: SIMF.Api.Tests/MatchRecommendationPushWorkerTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMF.Application.Notifications;
using SIMF.Application.Operations;
using SIMF.Application.Recommendations.Abstractions;
using SIMF.Common.Enums;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Operations;

/// <summary>
/// FR-803 (register defect <c>FR-803-80pct-push</c>) — the auto-recommendation
/// push. "Meet People Like You" was a pull-only surface: the ranker sorted by
/// score and took the top N, with no threshold anywhere and nothing that ever told
/// an attendee about a strong match. This worker is the push half; the threshold
/// itself lives with the ranker (<c>RecommendationService.StrongMatchThreshold</c>).
///
/// <para><b>Batched, not exhaustive.</b> The ranker does a full candidate scan per
/// caller, so scoring the whole roster in one tick would be O(n²) in a single
/// burst. Instead each tick takes <see cref="BatchSize"/> callers from a stable
/// user-id ordering, resuming from where the previous tick stopped and wrapping at
/// the end. Over a few hours the whole opted-in roster is covered; the cursor is
/// in-memory on purpose, because losing it on restart costs nothing (see dedup).</para>
///
/// <para><b>Dedup.</b> Once per (caller, candidate) pair, which is exactly what the
/// dispatcher guard gives —
/// <see cref="NotificationRequest.DeduplicateByRelatedEntity"/> with the
/// candidate's profile as the related entity. No stamp column, no claim/commit
/// dance: re-running a batch is a no-op, so a restart mid-pass is harmless.</para>
/// </summary>
internal sealed class MatchRecommendationPushWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IWorkerHeartbeatRegistry heartbeat,
    ILogger<MatchRecommendationPushWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);
    // First tick is delayed so the host finishes migrations + seeding before the
    // worker hits the DB — mirrors SessionReminderWorker.
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

    /// <summary>Callers scored per tick. Keeps one tick's work bounded regardless
    /// of roster size.</summary>
    internal const int BatchSize = 25;

    /// <summary>How far into the ordered opted-in roster the next batch starts.
    /// Zero restarts from the beginning.</summary>
    private int _cursor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "MatchRecommendationPushWorker started (first poll in {Delay}s, then every {Interval}s;"
            + " batch {Batch}).",
            (int)StartupDelay.TotalSeconds, (int)PollInterval.TotalSeconds, BatchSize);

        heartbeat.Register(
            nameof(MatchRecommendationPushWorker),
            "Pushes >=80% 'meet people like you' matches to opted-in attendees.",
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
                await PushOnceAsync(stoppingToken);
                heartbeat.RecordSuccess(nameof(MatchRecommendationPushWorker));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                heartbeat.RecordFailure(nameof(MatchRecommendationPushWorker), ex.Message);
                logger.LogError(ex, "MatchRecommendationPushWorker tick failed.");
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

    private async Task PushOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var recommendations = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        var batch = await NextBatchAsync(db, _cursor, BatchSize, cancellationToken);
        if (batch.Count < BatchSize)
        {
            // End of the roster — wrap, so the next tick picks the head up again.
            _cursor = 0;
        }
        else
        {
            _cursor += batch.Count;
        }
        if (batch.Count == 0)
        {
            return;
        }

        var pushed = await RunPushAsync(
            batch, recommendations, notifications, logger, cancellationToken);
        if (pushed > 0)
        {
            logger.LogInformation(
                "MatchRecommendationPushWorker pushed {Count} recommendation(s) over {Callers} caller(s).",
                pushed, batch.Count);
        }
    }

    /// <summary>One page of opted-in attendee user ids from a stable user-id
    /// ordering, starting <paramref name="skip"/> rows in. Only profiles that opted
    /// into "Meet People Like You" AND picked at least one interest can be matched at
    /// all, so nothing else ever enters a batch — the opt-out is enforced here, before
    /// the ranker sees the caller, as well as inside the ranker's own query.</summary>
    internal static Task<List<Guid>> NextBatchAsync(
        SimfAppDbContext db, int skip, int batchSize,
        CancellationToken cancellationToken) =>
        db.UserProfiles
            .AsNoTracking()
            .Where(p => p.ShowInMeetLikeYou && p.Interests.Any())
            .OrderBy(p => p.UserId)
            .Select(p => p.UserId)
            .Skip(skip)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The core pass, extracted for direct unit testing. Scores each caller and
    /// dispatches one invitation per candidate at or above the threshold. Returns
    /// the number of dispatches attempted. One caller's failure is logged and
    /// skipped — it never aborts the batch.
    /// </summary>
    internal static async Task<int> RunPushAsync(
        IReadOnlyList<Guid> callerUserIds,
        IRecommendationService recommendations,
        INotificationDispatcher notifications,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var pushed = 0;
        foreach (var callerUserId in callerUserIds)
        {
            try
            {
                var strong = await recommendations.StrongMatchesAsync(
                    callerUserId, cancellationToken);

                foreach (var match in strong.Matches)
                {
                    await notifications.DispatchAsync(new NotificationRequest
                    {
                        UserId = callerUserId,
                        Kind = NotificationKind.MatchRecommended,
                        Title = "Someone you should meet",
                        TitleArabic = "شخص يستحق أن تقابله",
                        Body = $"{match.EnglishName} shares your interests — {match.MatchReason}.",
                        BodyArabic = $"{match.ArabicName} يشاركك اهتماماتك — {match.MatchReasonArabic}.",
                        Severity = NotificationSeverity.Info,
                        RelatedEntityType = "UserProfile",
                        RelatedEntityId = match.UserProfileId,
                        // Once per (caller, candidate) — see the class remarks.
                        DeduplicateByRelatedEntity = true,
                        SendEmail = false,
                    }, cancellationToken);
                    pushed++;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "MatchRecommended push failed for caller {CallerUserId}", callerUserId);
            }
        }
        return pushed;
    }
}
