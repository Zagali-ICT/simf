namespace SIMF.Common;

/// <summary>
/// The whitelisted <c>SystemSetting</c> keys that drive the mobile
/// app-update policy served by <c>GET /api/v1/app/version-policy</c>. An admin
/// edits them on the CP configuration page (<c>/admin/configuration</c>); the
/// app compares its installed version against them on every launch and from the
/// About-the-app manual check. Versions are semver strings (e.g. "1.2.0");
/// store URLs are absolute http(s) listing links. An absent or blank key turns
/// that knob off (fail-open) — and without a store URL the app never gates or
/// prompts on that platform, so a misconfigured policy can never strand users
/// on a dead-end update screen.
/// </summary>
public static class AppUpdateSettingKeys
{
    public const string AndroidMinVersion = "appUpdate.android.minVersion";
    public const string AndroidLatestVersion = "appUpdate.android.latestVersion";
    public const string AndroidStoreUrl = "appUpdate.android.storeUrl";

    public const string IosMinVersion = "appUpdate.ios.minVersion";
    public const string IosLatestVersion = "appUpdate.ios.latestVersion";
    public const string IosStoreUrl = "appUpdate.ios.storeUrl";

    /// <summary>Every key the public read-path resolves — one query fetches
    /// them all, and the seeder pre-creates them so the CP grid is the menu of
    /// editable keys (no hand-typed key names).</summary>
    public static readonly IReadOnlyList<string> All =
    [
        AndroidMinVersion,
        AndroidLatestVersion,
        AndroidStoreUrl,
        IosMinVersion,
        IosLatestVersion,
        IosStoreUrl,
    ];
}
