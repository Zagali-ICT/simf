// Tests: SIMF.Api.Tests/FeedbackRatingsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Common.Enums;
using SIMF.Domain.Feedback;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Feedback;

/// <summary>
/// Seeds the two built-in (system) rating types — "App" (global) and "Session"
/// (per-session, with default Speaker / Sound / Light questions) — so the app's
/// App-rating entry and the end-of-session prompt resolve by code on a fresh
/// database. Idempotent and keyed on deterministic ids: re-running never
/// overwrites admin edits, it only inserts what is missing. Runs in every
/// environment (the types must exist in production too) and is invoked explicitly
/// by the test fixture, mirroring <c>IdentitySeeder</c>.
/// </summary>
public sealed class RatingSeeder(
    SimfAppDbContext appDbContext,
    TimeProvider timeProvider,
    ILogger<RatingSeeder> logger)
{
    // Deterministic ids so re-runs are idempotent and child rows link stably.
    private static readonly Guid AppTypeId = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid SessionTypeId = new("11111111-1111-1111-1111-000000000002");
    private static readonly Guid SessionSpeakerQuestionId = new("11111111-1111-1111-1111-000000000101");
    private static readonly Guid SessionSoundQuestionId = new("11111111-1111-1111-1111-000000000102");
    private static readonly Guid SessionLightQuestionId = new("11111111-1111-1111-1111-000000000103");

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var added = 0;

        added += await EnsureTypeAsync(new RatingType
        {
            Id = AppTypeId,
            Code = "App",
            Name = "App",
            NameArabic = "التطبيق",
            Scope = RatingScope.Global,
            HasOverallStars = true,
            AllowComment = true,
            IsSystem = true,
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = now,
        }, cancellationToken);

        added += await EnsureTypeAsync(new RatingType
        {
            Id = SessionTypeId,
            Code = "Session",
            Name = "Session",
            NameArabic = "الجلسة",
            Scope = RatingScope.PerSession,
            HasOverallStars = true,
            AllowComment = true,
            IsSystem = true,
            DisplayOrder = 1,
            IsActive = true,
            CreatedAt = now,
        }, cancellationToken);

        added += await EnsureQuestionAsync(SessionSpeakerQuestionId, "Speaker", "المتحدث", 0, now, cancellationToken);
        added += await EnsureQuestionAsync(SessionSoundQuestionId, "Sound", "الصوت", 1, now, cancellationToken);
        added += await EnsureQuestionAsync(SessionLightQuestionId, "Light", "الإضاءة", 2, now, cancellationToken);

        if (added > 0)
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Rating seed inserted {Count} built-in rating row(s).", added);
        }
    }

    private async Task<int> EnsureTypeAsync(RatingType type, CancellationToken cancellationToken)
    {
        if (await appDbContext.RatingTypes.AnyAsync(t => t.Id == type.Id, cancellationToken))
        {
            return 0;
        }
        appDbContext.RatingTypes.Add(type);
        return 1;
    }

    private async Task<int> EnsureQuestionAsync(
        Guid id, string text, string textArabic, int order, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await appDbContext.RatingQuestions.AnyAsync(q => q.Id == id, cancellationToken))
        {
            return 0;
        }
        appDbContext.RatingQuestions.Add(new RatingQuestion
        {
            Id = id,
            RatingTypeId = SessionTypeId,
            Text = text,
            TextArabic = textArabic,
            IsRequired = false,
            DisplayOrder = order,
            IsActive = true,
            CreatedAt = now,
        });
        return 1;
    }
}
