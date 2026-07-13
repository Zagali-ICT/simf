namespace SIMF.Common.Options;

/// <summary>
/// D-585 — settings for the demo user-account seed (the one-per-profile-type
/// sample accounts created by <c>IdentitySeeder.EnsureDemoAccountsAsync</c>).
/// Bound from the <c>Seed</c> configuration section.
/// <para><b>Security (Round-1 held item #1):</b> the demo accounts seed ONLY in
/// the Development environment or when <see cref="EnableDemoAccounts"/> is
/// explicitly set true — production is clean by construction. There is no
/// hardcoded password default: <see cref="DemoPassword"/> must be supplied by
/// per-environment config, and an empty value is the backstop that skips the
/// seed. These accounts MUST NOT exist in the production publish / NCA
/// handover.</para>
/// </summary>
public sealed class DemoSeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Round-1 held item #1 — opt IN to seeding the demo accounts in a
    /// non-Development environment (Development seeds them regardless). Defaults
    /// <c>false</c> so production never seeds them. Set via
    /// <c>Seed:EnableDemoAccounts</c>.</summary>
    public bool EnableDemoAccounts { get; set; }

    /// <summary>The single shared password for every seeded demo account.
    /// Supplied per-environment via <c>Seed:DemoPassword</c> (no committed
    /// default — an empty value skips the seed). Must satisfy the ASP.NET
    /// Identity password policy used by <c>accounts.CreateAsync</c>.</summary>
    public string DemoPassword { get; set; } = string.Empty;
}
