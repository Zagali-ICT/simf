namespace SIMF.Common;

/// <summary>
/// The Control Panel role names (P4). Defined once and never written as
/// literals elsewhere — adding a new CP role is a one-file change.
///
/// <para><see cref="Administrator"/> is the highest-trust role (super-admin
/// + staff promotion); <see cref="Staff"/> / <see cref="Scientific"/> /
/// <see cref="Security"/> are the reviewer roles (any team member approves
/// any visitor; only Administrator approves new staff).</para>
/// </summary>
public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string Staff = "Staff";
    public const string Scientific = "Scientific";
    public const string Security = "Security";

    /// <summary>
    /// Every CP-side role — the audience gate (P2) lets a user sign in to
    /// the Control Panel only when they hold at least one of these.
    /// </summary>
    public static readonly IReadOnlyList<string> CpRoles =
        [Administrator, Staff, Scientific, Security];
}
