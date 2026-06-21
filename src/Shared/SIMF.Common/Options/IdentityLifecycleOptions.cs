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
}
