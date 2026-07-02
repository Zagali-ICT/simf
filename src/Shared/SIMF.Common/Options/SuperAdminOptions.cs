namespace SIMF.Common.Options;

/// <summary>
/// The bootstrap super-administrator, bound from the <c>SuperAdmin</c>
/// configuration section. The temporary password and the TOTP secret are
/// supplied through the environment / <c>set-env</c> scripts and are never
/// committed (decision D4).
/// </summary>
public sealed class SuperAdminOptions
{
    public const string SectionName = "SuperAdmin";

    public string Email { get; set; } = string.Empty;

    public string TempPassword { get; set; } = string.Empty;

    public string TotpSecret { get; set; } = string.Empty;

    /// <summary>
    /// Whether the seeded super-admin is created with the one-time
    /// forced-password-change flag (D-059 / H19 / D-206). Defaults to
    /// <c>true</c> — the account must rotate the seed password on first CP
    /// login. A dev / test box may set
    /// <c>SIMF_SuperAdmin__PasswordChangeRequired=false</c> so the seed
    /// credential stays usable without the change step; leave it <c>true</c>
    /// for the production / NCA handover.
    /// </summary>
    public bool PasswordChangeRequired { get; set; } = true;
}
