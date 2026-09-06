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
///
/// <para><c>minVersion</c> is the one knob that is NOT read straight through: the
/// server withholds it until <c>minVersionEnforcedFrom</c> has arrived, which is
/// what turns a minimum into a grace period rather than a same-day fleet block.
/// A minimum with no date blocks nobody.</para>
/// </summary>
public static class AppUpdateSettingKeys
{
    public const string AndroidMinVersion = "appUpdate.android.minVersion";
    public const string AndroidLatestVersion = "appUpdate.android.latestVersion";
    public const string AndroidStoreUrl = "appUpdate.android.storeUrl";
    public const string AndroidMinVersionEnforcedFrom =
        "appUpdate.android.minVersionEnforcedFrom";

    public const string IosMinVersion = "appUpdate.ios.minVersion";
    public const string IosLatestVersion = "appUpdate.ios.latestVersion";
    public const string IosStoreUrl = "appUpdate.ios.storeUrl";
    public const string IosMinVersionEnforcedFrom =
        "appUpdate.ios.minVersionEnforcedFrom";

    /// <summary>The one date format the enforced-from keys are read in.
    /// Parsed with <c>TryParseExact</c> + <c>InvariantCulture</c> and nothing
    /// else: a culture-sensitive parse reads "06/10/2026" as June or October
    /// depending on the server's culture, which would be a fleet-wide block
    /// four months early.</summary>
    public const string EnforcedFromDateFormat = "yyyy-MM-dd";

    /// <summary>Every key the public read-path resolves — one query fetches
    /// them all, and the seeder pre-creates them so the CP grid is the menu of
    /// editable keys (no hand-typed key names).</summary>
    public static readonly IReadOnlyList<string> All =
    [
        AndroidMinVersion,
        AndroidLatestVersion,
        AndroidStoreUrl,
        AndroidMinVersionEnforcedFrom,
        IosMinVersion,
        IosLatestVersion,
        IosStoreUrl,
        IosMinVersionEnforcedFrom,
    ];
}
