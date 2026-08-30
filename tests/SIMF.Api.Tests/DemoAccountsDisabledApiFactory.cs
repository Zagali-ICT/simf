namespace SIMF.Api.Tests;

/// <summary>
/// A <see cref="SimfApiFactory"/> that seeds NO demo <c>@simf.local</c> account:
/// the fixture's own <see cref="DemoAccountSeeder"/> pass is switched off, and
/// <c>Seed:EnableDemoAccounts</c> is false so the host's
/// <c>DemoOperationalConfigSeeder</c> is a no-op too. What is left running is
/// exactly the production boot path, which is the point -
/// <see cref="DemoAccountSeedGateTests"/> uses this to prove that
/// <c>IdentitySeeder</c> creates no demo account of its own in any environment.
/// Only that class uses this fixture; the base factory re-enables both for every
/// other class.
/// </summary>
public sealed class DemoAccountsDisabledApiFactory : SimfApiFactory
{
    protected override bool SeedDemoAccounts => false;

    public DemoAccountsDisabledApiFactory()
    {
        // Runs AFTER the base constructor (which set it true), so the flag is
        // false for this factory's lifetime.
        Environment.SetEnvironmentVariable("Seed__EnableDemoAccounts", "false");
    }
}
