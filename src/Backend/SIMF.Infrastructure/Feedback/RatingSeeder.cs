// Tests: SIMF.Api.Tests/FeedbackRatingsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Common.Enums;
using SIMF.Domain.Feedback;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

namespace SIMF.Infrastructure.Feedback;

/// <summary>
/// Seeds the built-in (system) rating types — "App" and "Event" / "Exhibition"
/// (global, one per user), "Session" (per-session, with default Speaker / Sound /
/// Light questions) and "Day" (per programme day) — so the app's App-rating entry
/// and the end-of-session / end-of-day / end-of-programme prompts resolve by code
/// on a fresh database. Idempotent and keyed on deterministic ids:
/// re-running never
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
    private static readonly Guid DayTypeId = new("11111111-1111-1111-1111-000000000003");
    private static readonly Guid EventTypeId = new("11111111-1111-1111-1111-000000000004");
    private static readonly Guid ExhibitionTypeId = new("11111111-1111-1111-1111-000000000005");
    private static readonly Guid SessionSpeakerQuestionId = new("11111111-1111-1111-1111-000000000101");
    private static readonly Guid SessionSoundQuestionId = new("11111111-1111-1111-1111-000000000102");
    private static readonly Guid SessionLightQuestionId = new("11111111-1111-1111-1111-000000000103");

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.SimfNow();
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

        // End-of-day prompt target: one rating per user per programme day,
        // fired to attendees who checked in that day.
        added += await EnsureTypeAsync(new RatingType
        {
            Id = DayTypeId,
            Code = "Day",
            Name = "Day",
            NameArabic = "اليوم",
            Scope = RatingScope.PerDay,
            HasOverallStars = true,
            AllowComment = true,
            IsSystem = true,
            DisplayOrder = 2,
            IsActive = true,
            CreatedAt = now,
        }, cancellationToken);

        // Overall forum rating: one per user, fired at the end of the
        // whole programme.
        added += await EnsureTypeAsync(new RatingType
        {
            Id = EventTypeId,
            Code = "Event",
            Name = "Event",
            NameArabic = "الملتقى",
            Scope = RatingScope.Global,
            HasOverallStars = true,
            AllowComment = true,
            IsSystem = true,
            DisplayOrder = 3,
            IsActive = true,
            CreatedAt = now,
        }, cancellationToken);

        // Overall exhibition rating: one per user, fired at the end of the
        // whole programme.
        added += await EnsureTypeAsync(new RatingType
        {
            Id = ExhibitionTypeId,
            Code = "Exhibition",
            Name = "Exhibition",
            NameArabic = "المعرض",
            Scope = RatingScope.Global,
            HasOverallStars = true,
            AllowComment = true,
            IsSystem = true,
            DisplayOrder = 4,
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
        Guid id, string text, string textArabic, int order, DateTime now, CancellationToken cancellationToken)
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
