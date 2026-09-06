// Tests: SIMF.Api.Tests/AppVersionPolicyPublicTests.cs
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Configuration;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Configuration;

/// <summary>Resolves the mobile app-update policy from the whitelisted
/// <c>AppUpdateSettingKeys</c> rows in the system-settings store. Blank/absent
/// values surface as null ("rule off" — the app fails open); store URLs are
/// sanitised to absolute http(s)-only on read (the value becomes a
/// launched link on-device, so anything else drops to an inert null). Version
/// strings pass through as-is: the app owns semver parsing and ignores values
/// it cannot parse.
///
/// <para><b>The forced-update gate is decided HERE, not on the device.</b>
/// <c>minVersion</c> is emitted only once <c>minVersionEnforcedFrom</c> has
/// arrived, and null before it, which is what gives an operator a grace period.
/// Putting the date comparison in the app would have been useless on the day it
/// shipped: every build already in the field runs an evaluator that knows
/// nothing about the new key, would read <c>minVersion</c> and would block at
/// once — so the grace period would have applied only to people who had already
/// updated. Deciding on the server inverts that (old builds get it for free),
/// keeps the device clock out of it, and leaves the wire contract
/// untouched.</para></summary>
internal sealed class AppVersionPolicyService(
    SimfAppDbContext db,
    TimeProvider timeProvider) : IAppVersionPolicyService
{
    public async Task<AppVersionPolicyResponse> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var values = await db.SystemSettings.AsNoTracking()
            .Where(s => s.IsActive && AppUpdateSettingKeys.All.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        var today = DateOnly.FromDateTime(timeProvider.SimfNow());

        string? Version(string key) =>
            values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : null;

        string? StoreUrl(string key)
        {
            var value = Version(key);
            return value is not null
                && Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    ? value
                    : null;
        }

        // Absent, blank, deactivated, unparseable or still in the future all
        // mean "do not block anyone". Fail-open is not politeness here: the CP's
        // only off switch for a key is unticking IsActive (ConfigurationAddEdit
        // refuses to save an empty value), so under any other rule the one
        // gesture a panicking operator reaches for would brick the fleet
        // instead of rescuing it.
        bool Enforced(string key) =>
            Version(key) is { } raw
            && DateOnly.TryParseExact(
                raw,
                AppUpdateSettingKeys.EnforcedFromDateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var from)
            && today >= from;

        PlatformVersionPolicy Platform(
            string minKey, string latestKey, string storeKey, string enforcedFromKey)
        {
            var configuredMin = Version(minKey);
            // An operator who sets only the minimum still owes users a warning.
            // Without this they get silence for the whole grace window and then
            // a wall on the enforcement date — which is the anti-pattern the
            // grace period exists to avoid. Backfilled, they get the
            // dismissible prompt from day 0, snoozed on the app's own schedule.
            return new PlatformVersionPolicy(
                MinVersion: Enforced(enforcedFromKey) ? configuredMin : null,
                LatestVersion: Version(latestKey) ?? configuredMin,
                StoreUrl: StoreUrl(storeKey));
        }

        return new AppVersionPolicyResponse(
            Android: Platform(
                AppUpdateSettingKeys.AndroidMinVersion,
                AppUpdateSettingKeys.AndroidLatestVersion,
                AppUpdateSettingKeys.AndroidStoreUrl,
                AppUpdateSettingKeys.AndroidMinVersionEnforcedFrom),
            Ios: Platform(
                AppUpdateSettingKeys.IosMinVersion,
                AppUpdateSettingKeys.IosLatestVersion,
                AppUpdateSettingKeys.IosStoreUrl,
                AppUpdateSettingKeys.IosMinVersionEnforcedFrom));
    }
}
