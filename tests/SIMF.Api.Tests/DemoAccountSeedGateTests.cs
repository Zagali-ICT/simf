using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Identity;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Pins the owner rule that <c>IdentitySeeder</c> bootstraps identity and
/// NOTHING else: it creates the Control Panel roles, the permission catalogue
/// and one super-administrator, and no other account, in any environment.
///
/// <para>This used to test an environment GATE around a demo-account seed that
/// lived in the production seeder. The gate is gone because the seed is gone -
/// deleted rather than gated, so production is clean by construction rather than
/// by a flag someone could flip. The demo <c>@simf.local</c> matrix now belongs
/// to the test fixture (<see cref="DemoAccountSeeder"/>), and this class runs on
/// the one factory that switches that fixture pass off, so what remains is
/// exactly the production boot path.</para>
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class DemoAccountSeedGateTests : IClassFixture<DemoAccountsDisabledApiFactory>
{
    // The super-admin credential the factory configures (SuperAdmin:Email).
    private const string SuperAdminEmail = "superadmin@simf.test";

    private readonly DemoAccountsDisabledApiFactory _factory;

    public DemoAccountSeedGateTests(DemoAccountsDisabledApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task SeedAsync_creates_no_demo_account_and_still_bootstraps_the_super_admin()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();

        // Re-run the seed explicitly (idempotent) so the assertion is not
        // relying solely on the fixture's EnsureDatabaseCreated call.
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();

        // None of the nine demo @simf.local accounts exists: the production
        // seeder does not know about them at all any more.
        foreach (var demo in DemoAccountSeeder.Accounts)
        {
            Assert.Null(await users.FindByEmailAsync(demo.Email));
        }

        // The super-admin bootstrap is what the seeder is FOR, and it still runs.
        Assert.NotNull(await users.FindByEmailAsync(SuperAdminEmail));
    }
}
