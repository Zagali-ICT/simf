using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Proves a factory's host talks to that factory's OWN databases, even when
/// another factory was constructed in between.
/// </summary>
/// <remarks>
/// This is the suite's oldest and most expensive bug, and it never announced
/// itself as one. The fixture passes its two connection strings through
/// PROCESS-WIDE environment variables, while WebApplicationFactory builds its
/// host LAZILY on first use - so whichever factory was constructed last won for
/// any host not yet built, and that host then opened a database belonging to a
/// different factory. What came out was not a crash but a wrong answer: a class
/// querying someone else's database sees rows it never seeded and misses rows it
/// did, which is exactly the "suite interference" that has been blamed for
/// intermittent reds here for months (and, when the other factory had already
/// disposed, `Cannot open database ... the login failed` after a 90-second retry
/// storm).
///
/// The fix is `UseSetting` in ConfigureWebHost: it writes into that builder's own
/// configuration, which no other constructor can reach. The environment variables
/// are still set, because AddInfrastructure reads configuration eagerly - this
/// test is what proves the per-builder values are the ones that win.
/// </remarks>
[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class FactoryIsolationTests
{
    [Fact]
    public void A_host_built_after_another_factory_exists_uses_its_own_database()
    {
        using var first = new SimfApiFactory();

        // Constructing the second factory rewrites the process-wide connection
        // string variables to ITS databases. The first factory's host does not
        // exist yet, so this is the exact ordering that used to mis-point it.
        using var second = new SimfApiFactory();

        Assert.NotEqual(first.AppDatabaseName, second.AppDatabaseName);

        AssertUsesOwnDatabase(first);
        AssertUsesOwnDatabase(second);
    }

    private static void AssertUsesOwnDatabase(SimfApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var connection = scope.ServiceProvider
            .GetRequiredService<SimfAppDbContext>().Database.GetDbConnection();

        Assert.Equal(factory.AppDatabaseName, connection.Database);
    }
}
