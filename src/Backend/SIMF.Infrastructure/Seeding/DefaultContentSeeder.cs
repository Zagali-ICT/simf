// Tests: SIMF.Api.Tests/DefaultContentSeederTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Common;
using SIMF.Domain.Configuration;
using SIMF.Domain.Programme;
using SIMF.Domain.PublicRelations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Seeding;

/// <summary>
/// D-681 — seeds the forum's default public content on a fresh database so the
/// app is not empty on first boot (it fills the post-squash content gap): the
/// main hall, the three programme days (20-22 Nov 2026) with their sessions
/// (incl. the opening session), and one Highlights news item. Runs in every
/// environment and is idempotent — keyed on the natural keys (hall code, day
/// date, session code, the highlight marker title) so re-running only inserts
/// what is missing and never overwrites an admin edit. Mirrors
/// <c>RegionSeeder</c>. (The organisation profile — incl. its X / social links —
/// is owned by <c>IdentitySeeder</c>; D-708.)
///
/// <para>The opening session's live URL is a placeholder the owner replaces via
/// the Control Panel (it must pass <c>LiveStreamUrlPolicy</c>). The Highlights
/// image is attached via the Control Panel too — the seeder creates the article
/// row; the hero image is uploaded/linked by an editor.</para>
///
/// <para>D-736 — also pre-creates the six <c>AppUpdateSettingKeys</c> rows
/// (empty values) so the CP configuration grid is the menu of app-update policy
/// keys: an admin edits values in place instead of hand-typing exact key names
/// (a typo'd key is silently ignored by the whitelist read). Empty = that rule
/// off; existence is keyed on <c>Key</c> alone so a soft-deleted key is never
/// resurrected and an edited value never overwritten.</para>
/// </summary>
public sealed class DefaultContentSeeder(
    SimfAppDbContext appDbContext,
    TimeProvider timeProvider,
    ILogger<DefaultContentSeeder> logger)
{
    private const string HallCode = "MAIN";
    private const string HighlightTitle = "Saudi International Maritime Forum";

    // Placeholder YouTube live URL for the opening session (owner replaces via CP);
    // policy-valid so it survives LiveStreamUrlPolicy if it is ever re-validated.
    private const string OpeningLiveUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

    private static readonly TimeSpan Ast = TimeSpan.FromHours(3);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var changed = 0;

        var hallId = await EnsureHallAsync(now, cancellationToken);
        changed += await EnsureDaysAsync(now, cancellationToken);
        changed += await EnsureSessionsAsync(hallId, cancellationToken);
        changed += await EnsureHighlightAsync(now, cancellationToken);
        changed += await EnsureAppUpdateSettingsAsync(now, cancellationToken);

        if (changed > 0)
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Default content seed inserted/updated {Count} row(s).", changed);
        }
    }

    /// <summary>Ensure the single main hall exists; returns its id either way (a
    /// new hall is added to the change tracker, saved with everything else).</summary>
    private async Task<Guid> EnsureHallAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await appDbContext.Halls
            .Where(h => h.Code == HallCode)
            .Select(h => (Guid?)h.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is { } id)
        {
            return id;
        }

        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = HallCode,
            Name = "Main Hall",
            NameArabic = "القاعة الرئيسية",
            Capacity = 500,
            IsActive = true,
            CreatedAt = now,
        };
        appDbContext.Halls.Add(hall);
        return hall.Id;
    }

    private async Task<int> EnsureDaysAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var days = new (DateOnly Date, string Title, string TitleArabic, int Order)[]
        {
            (new DateOnly(2026, 11, 20), "Day One", "اليوم الأول", 0),
            (new DateOnly(2026, 11, 21), "Day Two", "اليوم الثاني", 1),
            (new DateOnly(2026, 11, 22), "Day Three", "اليوم الثالث", 2),
        };

        var wanted = days.Select(d => d.Date).ToList();
        var present = await appDbContext.ProgrammeDays
            .Where(d => wanted.Contains(d.Date))
            .Select(d => d.Date)
            .ToListAsync(cancellationToken);
        var have = present.ToHashSet();

        var added = 0;
        foreach (var (date, title, titleArabic, order) in days)
        {
            if (have.Contains(date))
            {
                continue;
            }
            appDbContext.ProgrammeDays.Add(new ProgrammeDay
            {
                Id = Guid.NewGuid(),
                Date = date,
                Title = title,
                TitleArabic = titleArabic,
                DisplayOrder = order,
                IsActive = true,
                CreatedAt = now,
            });
            added++;
        }
        return added;
    }

    private async Task<int> EnsureSessionsAsync(Guid hallId, CancellationToken cancellationToken)
    {
        var sessions = new SessionSeed[]
        {
            new("S-D1-01", "Opening Session", "الجلسة الافتتاحية", Local(20, 9, 0), Local(20, 10, 0), OpeningLiveUrl),
            new("S-D1-02", "Panel Session", "جلسة حوارية", Local(20, 11, 0), Local(20, 12, 30), null),
            new("S-D2-01", "Morning Session", "الجلسة الصباحية", Local(21, 9, 0), Local(21, 10, 30), null),
            new("S-D2-02", "Afternoon Session", "الجلسة المسائية", Local(21, 11, 0), Local(21, 12, 30), null),
            new("S-D3-01", "Closing Session", "الجلسة الختامية", Local(22, 9, 0), Local(22, 10, 30), null),
        };

        var wanted = sessions.Select(s => s.Code).ToList();
        var present = await appDbContext.Sessions
            .Where(s => wanted.Contains(s.Code))
            .Select(s => s.Code)
            .ToListAsync(cancellationToken);
        var have = present.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var seed in sessions)
        {
            if (have.Contains(seed.Code))
            {
                continue;
            }
            appDbContext.Sessions.Add(new Session
            {
                Id = Guid.NewGuid(),
                Code = seed.Code,
                Title = seed.Title,
                TitleArabic = seed.TitleArabic,
                HallId = hallId,
                StartUtc = seed.Start,
                EndUtc = seed.End,
                LiveStreamUrl = seed.LiveUrl,
                IsActive = true,
                CreatedAt = seed.Start,
            });
            added++;
        }
        return added;
    }

    private async Task<int> EnsureHighlightAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await appDbContext.News.AnyAsync(n => n.Title == HighlightTitle, cancellationToken))
        {
            return 0;
        }
        appDbContext.News.Add(new News
        {
            Id = Guid.NewGuid(),
            Title = HighlightTitle,
            TitleArabic = "الملتقى البحري السعودي الدولي",
            Excerpt = "The Kingdom's flagship maritime and naval forum.",
            ExcerptArabic = "الحدث البحري والدفاعي الأبرز في المملكة.",
            Body = "The Saudi International Maritime Forum brings together the maritime and "
                + "defence community for three days of sessions, exhibitions and networking.",
            BodyArabic = "يجمع الملتقى البحري السعودي الدولي المجتمع البحري والدفاعي على مدى "
                + "ثلاثة أيام من الجلسات والمعارض وفرص التواصل.",
            Category = "Highlights",
            CategoryArabic = "أبرز الأحداث",
            PublishedAt = now,
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = now,
        });
        return 1;
    }

    /// <summary>D-736 — pre-creates the app-update policy keys (empty values)
    /// so they show up on the CP configuration grid ready to edit. Existence is
    /// keyed on <c>Key</c> alone (IsActive ignored): a re-run never overwrites
    /// an admin edit and never resurrects a deliberately deactivated key.</summary>
    private async Task<int> EnsureAppUpdateSettingsAsync(
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var descriptions = new Dictionary<string, string>
        {
            [AppUpdateSettingKeys.AndroidMinVersion] =
                "Minimum supported Android app version (semver, e.g. 1.2.0). "
                + "Older installs are blocked until they update. Empty = no forced-update gate.",
            [AppUpdateSettingKeys.AndroidLatestVersion] =
                "Latest released Android app version (semver, e.g. 1.4.0). "
                + "Older installs get a dismissible update prompt. Empty = no prompt.",
            [AppUpdateSettingKeys.AndroidStoreUrl] =
                "Google Play listing URL the app's Update button opens (absolute https). "
                + "Empty disables both the forced gate and the prompt on Android.",
            [AppUpdateSettingKeys.IosMinVersion] =
                "Minimum supported iOS app version (semver, e.g. 1.2.0). "
                + "Older installs are blocked until they update. Empty = no forced-update gate.",
            [AppUpdateSettingKeys.IosLatestVersion] =
                "Latest released iOS app version (semver, e.g. 1.4.0). "
                + "Older installs get a dismissible update prompt. Empty = no prompt.",
            [AppUpdateSettingKeys.IosStoreUrl] =
                "App Store listing URL the app's Update button opens (absolute https). "
                + "Empty disables both the forced gate and the prompt on iOS.",
        };

        var wanted = AppUpdateSettingKeys.All.ToList();
        var present = await appDbContext.SystemSettings
            .Where(s => wanted.Contains(s.Key))
            .Select(s => s.Key)
            .ToListAsync(cancellationToken);
        // Case-insensitive to match the SystemSettings.Key unique index (SQL
        // Server default collation) — an admin-created row differing only in
        // case must count as present, or the add below would collide on insert.
        var have = present.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var key in wanted)
        {
            if (have.Contains(key))
            {
                continue;
            }
            appDbContext.SystemSettings.Add(new SystemSetting
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = string.Empty,
                Description = descriptions[key],
                IsActive = true,
                CreatedAt = now,
            });
            added++;
        }
        return added;
    }

    /// <summary>An event-local (UTC+3) session time on a November-2026 day.</summary>
    private static DateTimeOffset Local(int day, int hour, int minute) =>
        new(2026, 11, day, hour, minute, 0, Ast);

    private readonly record struct SessionSeed(
        string Code, string Title, string TitleArabic,
        DateTimeOffset Start, DateTimeOffset End, string? LiveUrl);
}
