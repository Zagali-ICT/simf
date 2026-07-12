// Tests: SIMF.Api.Tests/DefaultContentSeederTests.cs
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
/// <para>D-736 — pre-creates the six <c>AppUpdateSettingKeys</c> rows (empty
/// values) so the CP configuration grid is the menu of app-update policy keys:
/// an admin edits values in place instead of hand-typing exact key names (a
/// typo'd key is silently ignored by the whitelist read). Empty = that rule
/// off; existence is keyed on <c>Key</c> alone so a soft-deleted key is never
/// resurrected and an edited value never overwritten. Runs in every
/// environment and is idempotent.</para>
///
/// <para>D-747 — the 2026 event CONTENT this seeder used to create (the main
/// hall, the programme days + sessions, and the Highlights news item — D-681)
/// moved out of C# into the by-hand SQL lane (<c>docs/migrations/2026/*.sql</c>,
/// owner rule D-718). In Development/Testing those files are applied by
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
        var now = timeProvider.GetUtcNow();
        var changed = await EnsureAppUpdateSettingsAsync(now, cancellationToken);

        if (changed > 0)
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Default config seed inserted/updated {Count} row(s).", changed);
        }
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
}
