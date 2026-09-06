// Tests: SIMF.Api.Tests/DefaultContentSeederTests.cs,
//        SIMF.Api.Tests/SeedRaceGuardTests.cs (the concurrent first-boot
//        tolerance this seeder now saves through)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Common;
using SIMF.Domain.Configuration;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Seeding;

/// <summary>
/// Seeds the app-update policy config keys so the CP configuration grid is not
/// empty on a fresh database.
///
/// <para>Pre-creates every <c>AppUpdateSettingKeys</c> row (empty
/// values) so the CP configuration grid is the menu of app-update policy keys:
/// an admin edits values in place instead of hand-typing exact key names (a
/// typo'd key is silently ignored by the whitelist read). Empty = that rule
/// off; existence is keyed on <c>Key</c> alone so a soft-deleted key is never
/// resurrected and an edited value never overwritten. Runs in every
/// environment and is idempotent.</para>
///
/// <para>The 2026 event CONTENT this seeder used to create (the main
/// hall, the programme days + sessions, and the Highlights news item)
/// moved out of C# into the by-hand SQL lane
/// (<c>docs/migrations/2026/*.sql</c>). In Development/Testing those files are applied by
/// <see cref="SqlContentSeeder"/>; in production they are run by hand. The org
/// profile (incl. its social links) is owned by <c>IdentitySeeder</c>.</para>
/// </summary>
public sealed class DefaultContentSeeder(
    SimfAppDbContext appDbContext,
    TimeProvider timeProvider,
    ILogger<DefaultContentSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.SimfNow();
        var changed = await EnsureAppUpdateSettingsAsync(now, cancellationToken);

        if (changed == 0)
        {
            return;
        }

        // Tolerates a concurrent first boot: several API instances run this
        // seeder against the same empty database, so the unique index on
        // SystemSettings.Key rejects whichever one arrives second. The log line
        // below is gated on the result because its row count would be untrue on
        // the instance that lost.
        if (await appDbContext.SaveToleratingFirstBootRaceAsync(
            logger, "Default config seed", cancellationToken))
        {
            logger.LogInformation("Default config seed inserted/updated {Count} row(s).", changed);
        }
    }

    /// <summary>Pre-creates the app-update policy keys (empty values)
    /// so they show up on the CP configuration grid ready to edit. Existence is
    /// keyed on <c>Key</c> alone (IsActive ignored): a re-run never overwrites
    /// an admin edit and never resurrects a deliberately deactivated key.</summary>
    private async Task<int> EnsureAppUpdateSettingsAsync(
        DateTime now, CancellationToken cancellationToken)
    {
        var descriptions = new Dictionary<string, string>
        {
            [AppUpdateSettingKeys.AndroidMinVersion] =
                "Minimum supported Android app version (semver, e.g. 1.2.0). "
                + "Blocks older installs, but ONLY from the date in "
                + "appUpdate.android.minVersionEnforcedFrom. Empty = no forced-update gate.",
            [AppUpdateSettingKeys.AndroidLatestVersion] =
                "Latest released Android app version (semver, e.g. 1.4.0). "
                + "Older installs get a dismissible update prompt. "
                + "Empty falls back to the minimum version above, so setting only a "
                + "minimum still warns users during the grace period.",
            [AppUpdateSettingKeys.AndroidStoreUrl] =
                "Google Play listing URL the app's Update button opens (absolute https). "
                + "Empty disables both the forced gate and the prompt on Android.",
            [AppUpdateSettingKeys.AndroidMinVersionEnforcedFrom] = EnforcedFromDescription(
                AppUpdateSettingKeys.AndroidMinVersion, "a Play staged rollout"),
            [AppUpdateSettingKeys.IosMinVersion] =
                "Minimum supported iOS app version (semver, e.g. 1.2.0). "
                + "Blocks older installs, but ONLY from the date in "
                + "appUpdate.ios.minVersionEnforcedFrom. Empty = no forced-update gate.",
            [AppUpdateSettingKeys.IosLatestVersion] =
                "Latest released iOS app version (semver, e.g. 1.4.0). "
                + "Older installs get a dismissible update prompt. "
                + "Empty falls back to the minimum version above, so setting only a "
                + "minimum still warns users during the grace period.",
            [AppUpdateSettingKeys.IosStoreUrl] =
                "App Store listing URL the app's Update button opens (absolute https). "
                + "Empty disables both the forced gate and the prompt on iOS.",
            [AppUpdateSettingKeys.IosMinVersionEnforcedFrom] = EnforcedFromDescription(
                AppUpdateSettingKeys.IosMinVersion, "iOS Phased Release"),
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

    /// <summary>The operator-facing description of an enforced-from key. It
    /// carries the date format and the rollout rule because
    /// <c>/admin/configuration</c> is generic key/value CRUD with no per-key
    /// validation and no help text of its own — this string is the only place
    /// an admin is told either. Both matter: a date in any other format is
    /// silently ignored (the gate stays off, which is safe but confusing), and
    /// enforcing while a rollout is still staged blocks users who cannot yet
    /// install the build they are being told to install.</summary>
    private static string EnforcedFromDescription(string minVersionKey, string rollout) =>
        $"Date from which {minVersionKey} is actually enforced, as "
        + $"{AppUpdateSettingKeys.EnforcedFromDateFormat} (e.g. 2026-10-15). "
        + "Empty, inactive or any other format = the minimum is NOT enforced and "
        + "nobody is blocked; a past date enforces immediately. Until this date "
        + "users only get the dismissible update prompt. Never set it while "
        + $"{rollout} is still in progress, and allow at least 14 days.";
}
