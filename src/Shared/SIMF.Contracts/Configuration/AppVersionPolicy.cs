namespace SIMF.Contracts.Configuration;

/// <summary>One platform's app-update policy: the minimum supported
/// version (older installs are hard-blocked until updated), the latest released
/// version (older installs get a dismissible prompt), and the store listing URL
/// the Update button opens. Every field is null when not configured — the app
/// treats a null as "that rule is off" (fail-open).</summary>
public sealed record PlatformVersionPolicy(
    string? MinVersion,
    string? LatestVersion,
    string? StoreUrl);

/// <summary>The mobile app-update policy served by
/// <c>GET /api/v1/app/version-policy</c>, admin-edited via the whitelisted
/// <c>AppUpdateSettingKeys</c> on the CP configuration page. Both platforms ship
/// in one payload; the app picks its own side and compares versions locally.</summary>
public sealed record AppVersionPolicyResponse(
    PlatformVersionPolicy Android,
    PlatformVersionPolicy Ios);
