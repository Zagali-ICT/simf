namespace SIMF.Common.Options;

/// <summary>
/// A7-13 (NCA Secure Application-Development Standard) — credential-lifecycle
/// settings, bound from the <c>IdentityLifecycle</c> configuration section.
/// </summary>
public sealed class IdentityLifecycleOptions
{
    public const string SectionName = "IdentityLifecycle";

    /// <summary>
    /// Maximum password age in days. When greater than zero, a sign-in whose
    /// password is older than this forces a password change (via the existing
    /// forced-change flow). <c>0</c> (the default) disables expiry, so the
    /// mechanism is present and admin-configurable per NCA A7-13 without forcing
    /// a fleet-wide reset until the owner sets a value.
    /// </summary>
    public int PasswordMaxAgeDays { get; set; }

    /// <summary>
    /// A7-20 — how many previous passwords are disallowed on a change / reset.
    /// When greater than zero, the new password is rejected if it matches the
    /// current password or any of the most recent <c>PasswordHistoryCount</c>
    /// retired passwords. <c>0</c> (the default) disables the check (and the
    /// recording), so the feature is admin-configurable per NCA A7-20.
    /// </summary>
    public int PasswordHistoryCount { get; set; }

    /// <summary>
    /// A1-19 — after how many days of inactivity an Approved account is
    /// automatically disabled by the daily sweep. Inactivity is measured from the
    /// last successful sign-in (or the account creation time if it never signed
    /// in). <c>0</c> (the default) disables the sweep, so the control is
    /// admin-configurable per NCA A1-19 without disabling accounts until the owner
    /// sets a value.
    /// </summary>
    public int DormantAccountDisableDays { get; set; }

    /// <summary>
    /// #2 (Q1, 2026-07-30) — when <c>true</c> (the default), a sign-in on the
    /// <c>Cp</c> audience whose account has no authenticator secret paired is
    /// answered with a mandatory-enrolment challenge instead of an access token,
    /// so the Control Panel never mints a session on the password alone. The App
    /// and Web audiences are never affected by this setting.
    ///
    /// <para>Secure by default: an absent configuration key leaves this at
    /// <c>true</c>. It exists as a switch only so the general integration suite —
    /// whose admin fixtures create users straight through <c>UserManager</c> and
    /// predate enrolment-first — can pin it off, exactly as it already pins
    /// <c>FaceDetection:Enabled</c> and <c>DeviceKey:RequireStepUpForEnrol</c>.
    /// The production posture is proved by the tests that turn it back on
    /// (<c>ControlPanelTwoFactorEnrolmentTests</c>).</para>
    /// </summary>
    public bool RequireControlPanelTwoFactorEnrolment { get; set; } = true;
}


