// Tests: SIMF.Api.Tests/AppVersionPolicyPublicTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Configuration;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Configuration;

/// <summary>D-736 — resolves the mobile app-update policy from the whitelisted
/// <c>AppUpdateSettingKeys</c> rows in the system-settings store. Blank/absent
/// values surface as null ("rule off" — the app fails open); store URLs are
/// sanitised to absolute http(s)-only on read (D-467 — the value becomes a
/// launched link on-device, so anything else drops to an inert null). Version
/// strings pass through as-is: the app owns semver parsing and ignores values
/// it cannot parse.</summary>
internal sealed class AppVersionPolicyService(SimfAppDbContext db) : IAppVersionPolicyService
{
    public async Task<AppVersionPolicyResponse> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var values = await db.SystemSettings.AsNoTracking()
            .Where(s => s.IsActive && AppUpdateSettingKeys.All.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

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

        return new AppVersionPolicyResponse(
            Android: new PlatformVersionPolicy(
                MinVersion: Version(AppUpdateSettingKeys.AndroidMinVersion),
                LatestVersion: Version(AppUpdateSettingKeys.AndroidLatestVersion),
                StoreUrl: StoreUrl(AppUpdateSettingKeys.AndroidStoreUrl)),
            Ios: new PlatformVersionPolicy(
                MinVersion: Version(AppUpdateSettingKeys.IosMinVersion),
                LatestVersion: Version(AppUpdateSettingKeys.IosLatestVersion),
                StoreUrl: StoreUrl(AppUpdateSettingKeys.IosStoreUrl)));
    }
}
