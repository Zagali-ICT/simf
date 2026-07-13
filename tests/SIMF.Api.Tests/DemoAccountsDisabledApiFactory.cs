namespace SIMF.Api.Tests;

/// <summary>
/// Round-1 held item #1 — a <see cref="SimfApiFactory"/> with the demo-account
/// seed switched OFF (<c>Seed:EnableDemoAccounts = false</c>). The host runs as
/// the non-Development "Testing" environment, so with the flag off the gate in
/// <c>IdentitySeeder.SeedAsync</c> must make the demo @simf.local seed a no-op —
/// exactly the production posture. Only <see cref="DemoAccountSeedGateTests"/>
/// uses this fixture; the base factory re-enables the flag for every other class.
/// </summary>
public sealed class DemoAccountsDisabledApiFactory : SimfApiFactory
{
    public DemoAccountsDisabledApiFactory()
    {
        // Runs AFTER the base constructor (which set it true), so the flag is
        // false for this factory's lifetime.
        Environment.SetEnvironmentVariable("Seed__EnableDemoAccounts", "false");
    }
}
