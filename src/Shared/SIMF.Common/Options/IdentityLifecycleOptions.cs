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
    /// <c>true</c>, and since 2026-07-31 <b>nothing turns it off</b> — not even the
    /// tests. The general integration suite used to pin it off, which meant its
    /// ~150 admin fixtures exercised the pre-fix single-factor path; those fixtures
    /// now enrol an authenticator and complete a real TOTP step
    /// (<c>AuthFlow.SignInControlPanelAsync</c>). That the gate is genuinely on for
    /// that suite is asserted by <c>ControlPanelTwoFactorGatePinTests</c>, so
    /// pinning it off again fails the build rather than silently reverting the
    /// posture; the enrolment contract itself is proved by
    /// <c>ControlPanelTwoFactorEnrolmentTests</c>.</para>
    /// </summary>
    public bool RequireControlPanelTwoFactorEnrolment { get; set; } = true;
}


