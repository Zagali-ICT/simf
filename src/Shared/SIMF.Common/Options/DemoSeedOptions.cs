namespace SIMF.Common.Options;

/// <summary>
/// D-585 — settings for the demo user-account seed (the one-per-profile-type
/// sample accounts created by <c>IdentitySeeder.EnsureDemoAccountsAsync</c>).
/// Bound from the <c>Seed</c> configuration section.
/// <para><b>Security note (owner decision D-585):</b> the demo accounts seed in
/// EVERY environment, including production, and all share one password. That is
/// a deliberate owner choice for a demo build; the password is sourced here so
/// an operator can override it per-environment via <c>Seed:DemoPassword</c>
/// rather than it being hard-committed. These accounts (and this default) MUST
/// be removed / rotated before the production publish + NCA handover.</para>
/// </summary>
public sealed class DemoSeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>The single shared password for every seeded demo account.
    /// Override via <c>Seed:DemoPassword</c>. Must satisfy the ASP.NET Identity
    /// password policy used by <c>accounts.CreateAsync</c>.</summary>
    public string DemoPassword { get; set; } = "Simf@Demo2026#";
}
