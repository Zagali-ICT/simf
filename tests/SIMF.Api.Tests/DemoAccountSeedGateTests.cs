using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Identity;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Round-1 held item #1 — proves the demo-account seed (D-585) is a no-op when
/// the host is not Development and <c>Seed:EnableDemoAccounts</c> is off, so
/// production never carries the pre-known demo credentials (including the
/// Administrator-role <c>admin@simf.local</c>). The super-admin bootstrap is
/// untouched by the gate and must still seed.
/// </summary>
public sealed class DemoAccountSeedGateTests : IClassFixture<DemoAccountsDisabledApiFactory>
{
    // The super-admin credential the factory configures (SuperAdmin:Email).
    private const string SuperAdminEmail = "superadmin@simf.test";

    private static readonly string[] DemoEmails =
    {
        "admin@simf.local", "vvip@simf.local", "vip@simf.local", "visitor@simf.local",
        "staff@simf.local", "moderator@simf.local", "exhibitor@simf.local",
        "media@simf.local", "sponsor@simf.local",
    };

    private readonly DemoAccountsDisabledApiFactory _factory;

    public DemoAccountSeedGateTests(DemoAccountsDisabledApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task SeedAsync_does_not_seed_demo_accounts_outside_development_with_flag_off()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();

        // Re-run the seed explicitly (idempotent) so the assertion is not
        // relying solely on the fixture's EnsureDatabaseCreated call.
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();

        // The gate (host is "Testing", flag off) makes EnsureDemoAccountsAsync a
        // no-op — none of the nine demo @simf.local accounts exist.
        foreach (var email in DemoEmails)
        {
            Assert.Null(await users.FindByEmailAsync(email));
        }

        // The super-admin bootstrap is unchanged by the gate — it still seeds.
        Assert.NotNull(await users.FindByEmailAsync(SuperAdminEmail));
    }
}
