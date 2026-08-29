namespace SIMF.Common;

/// <summary>
/// The whitelisted <c>SystemSetting</c> keys that let a Control Panel admin turn
/// the two walk-in desk modes on and off DURING an event, without a deploy.
///
/// <para><b>These two keys override configuration; they do not replace it.</b>
/// <c>WalkInModeOptions</c> is still bound from <c>appsettings</c> /
/// <c>SIMF_API_*</c>, and a key that is absent or blank leaves that flag reading
/// whatever configuration says. Only an explicit "true"/"false" row wins. That
/// ordering matters on a fresh database, where no rows exist and the estate's
/// configured posture must survive untouched.</para>
///
/// <para><b>The master switch is deliberately NOT here.</b> Both modes resolve as
/// <c>IsArmed(now) &amp;&amp; flag</c>, and <c>IsArmed</c> — walk-in mode enabled, inside
/// its window — stays in deployment configuration. So an admin may turn
/// auto-approve off in the middle of a rush, but cannot arm walk-in registration
/// on an estate that never enabled it. Relaxing the approval gate that far still
/// costs server access, which is a stronger control than any CP permission, and
/// the CP page shows the armed state so a toggle that is currently inert says so
/// rather than lying about its effect.</para>
/// </summary>
public static class WalkInModeSettingKeys
{
    /// <summary>Overrides <c>WalkInModeOptions.QuickRegister</c> — the reduced
    /// desk field set (name + one identity document).</summary>
    public const string QuickRegister = "walkInMode.quickRegister";

    /// <summary>Overrides <c>WalkInModeOptions.AutoApprove</c> — an on-site
    /// audience registration is approved and given its QR at the desk instead of
    /// queueing for an administrator.</summary>
    public const string AutoApprove = "walkInMode.autoApprove";

    /// <summary>Every key the read-path resolves, so one query fetches them all
    /// and the seeder can pre-create them: the CP grid then offers the editable
    /// keys as a menu rather than asking anyone to type a key name.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        QuickRegister,
        AutoApprove,
    ];

    /// <summary>Parses a stored value into an explicit override. Anything that is
    /// not recognisably a boolean returns null and defers to configuration, so a
    /// hand-edited row can never turn a mode on by accident.</summary>
    public static bool? ParseOverride(string? value) =>
        bool.TryParse(value?.Trim(), out var parsed) ? parsed : null;
}
