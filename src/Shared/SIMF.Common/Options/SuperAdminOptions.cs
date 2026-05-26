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
}
