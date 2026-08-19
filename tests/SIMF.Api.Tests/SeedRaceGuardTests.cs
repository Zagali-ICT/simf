// Concurrent first boot. Every seeder reads what is present and inserts what is
// missing, and the host runs the whole sequence on EVERY API instance at
// startup with no lease around it. Two instances cold-starting against the same
// empty database therefore read the same empty tables and insert the same rows,
// and the unique index rejects whichever one arrives second. Before
// SeedRaceGuard that rejection propagated out of the host's seed block and the
// losing instance failed to start over work that had in fact succeeded.
//
// What these tests can and cannot reach. Program.cs skips the whole seed block
// under the "Testing" environment, so no test can drive two real instances
// through a genuine first boot; the race itself is NOT covered here. What is
// covered is the part the fix turns on: that a real SQL Server duplicate key is
// recognised, that a store failure which is not a duplicate is still fatal, and
// that after a lost race the context is left in a state where the seeder's next
// step still commits - which is "the losing node keeps booting" in miniature.
//
// The true-positive case uses a real duplicate against the fixture's LocalDB
// rather than a fabricated exception, because SqlException cannot be constructed
// and a hand-rolled stand-in would prove only that the test agrees with itself.
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SIMF.Common;
using SIMF.Domain.Configuration;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using SIMF.Infrastructure.Seeding;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class SeedRaceGuardTests : IClassFixture<SimfApiFactory>
{
    private const string TestSeedLabel = "Seed-race guard test";

    private readonly SimfApiFactory _factory;

    public SeedRaceGuardTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    /// <summary>The true positive, taken from the real store: inserting a key
    /// that is already there is what a losing instance actually experiences, and
    /// the guard has to recognise it from the SQL Server error number.</summary>
    [Fact]
    public async Task A_duplicate_key_from_the_real_store_is_recognised_as_a_lost_race()
    {
        var contestedKey = NewKey();
        await InsertSettingAsync(contestedKey);

        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(
            () => InsertSettingAsync(contestedKey));

        // Named rather than asserted through the guard alone, so a failure says
        // which error SQL Server actually reported instead of only that the
        // guard disagreed with it.
        var storeErrorNumber = (duplicate.InnerException as SqlException)?.Number;
        Assert.True(
            storeErrorNumber is 2601 or 2627,
            "The duplicate did not arrive as a SQL Server uniqueness error, so the "
            + "guard is matching on the wrong number. The store reported: "
            + storeErrorNumber);
        Assert.True(SeedRaceGuard.IsUniqueIndexViolation(duplicate));
    }

    /// <summary>The whole point of the tolerance: the instance that lost the race
    /// carries on and its NEXT seed step still commits. Before this, the rejected
    /// rows stayed tracked as Added and poisoned every save that followed.</summary>
    [Fact]
    public async Task The_losing_instance_drops_the_refused_rows_and_keeps_seeding()
    {
        var contestedKey = NewKey();
        await InsertSettingAsync(contestedKey);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.SystemSettings.Add(NewSetting(contestedKey));

        var saved = await db.SaveToleratingFirstBootRaceAsync(
            NullLogger.Instance, TestSeedLabel, CancellationToken.None);

        Assert.False(saved);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), entry => entry.State == EntityState.Added);

        // The step after the lost race commits normally - the losing node boots.
        var ownKey = NewKey();
        db.SystemSettings.Add(NewSetting(ownKey));
        await db.SaveChangesAsync(CancellationToken.None);

        Assert.True(await db.SystemSettings.AnyAsync(setting => setting.Key == ownKey));
        // The contested key is present exactly once: the winner's row, not two.
        Assert.Equal(1, await db.SystemSettings.CountAsync(setting => setting.Key == contestedKey));
    }

    /// <summary>The guard stays narrow. A store failure that is not a duplicate
    /// must still fail the boot, because reporting a silent success would leave
    /// an operator with a half-seeded database and nothing in the log to say
    /// so - a worse outcome than an instance that refuses to start.</summary>
    [Fact]
    public async Task A_store_failure_that_is_not_a_duplicate_still_fails_the_boot()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        // A role grant pointing at a role and a permission that do not exist:
        // a genuine write failure (a foreign key rejection), with nothing to do
        // with another instance having got there first.
        db.RolePermissions.Add(new RolePermission
        {
            RoleId = Guid.NewGuid(),
            PermissionId = Guid.NewGuid(),
        });

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(
            async () => await db.SaveToleratingFirstBootRaceAsync(
                NullLogger.Instance, TestSeedLabel, CancellationToken.None));

        Assert.False(SeedRaceGuard.IsUniqueIndexViolation(thrown));
    }

    /// <summary>The negatives the guard must reject without touching a database.
    /// The concurrency case matters most: a lost DELETE race carries no store
    /// error at all, so an insert-side guard that claimed it would swallow every
    /// stale-row failure in the system.</summary>
    [Fact]
    public void An_update_failure_carrying_no_store_uniqueness_error_is_not_a_lost_race()
    {
        Assert.False(SeedRaceGuard.IsUniqueIndexViolation(
            new DbUpdateException("The connection dropped mid-write.")));

        Assert.False(SeedRaceGuard.IsUniqueIndexViolation(
            new DbUpdateException(
                "Wrapped a failure that never reached the store.",
                new InvalidOperationException("not a store error"))));

        Assert.False(SeedRaceGuard.IsUniqueIndexViolation(
            new DbUpdateConcurrencyException(
                "The database operation was expected to affect 1 row(s) but actually affected 0.")));
    }

    // ---------------------------------------------------------------------

    /// <summary>A key no other test writes, so a shared fixture database cannot
    /// make one of these tests pass or fail for another test's reasons.</summary>
    private static string NewKey() => $"seedRace.{Guid.NewGuid():N}";

    private static SystemSetting NewSetting(string key) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        Value = string.Empty,
        Description = "Written by SeedRaceGuardTests.",
        IsActive = true,
        CreatedAt = SimfClock.Now,
    };

    /// <summary>Inserts one settings row on its own scope, so the row is
    /// committed and tracked nowhere the caller can see - which is what makes the
    /// second insert a genuine store-level duplicate rather than a
    /// change-tracker complaint.</summary>
    private async Task InsertSettingAsync(string key)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.SystemSettings.Add(NewSetting(key));
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
