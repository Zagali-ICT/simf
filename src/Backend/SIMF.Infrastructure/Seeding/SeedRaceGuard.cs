// Tests: SIMF.Api.Tests/SeedRaceGuardTests.cs
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SIMF.Infrastructure.Seeding;

/// <summary>
/// The one write failure a seeder is allowed to tolerate: another instance
/// inserting the same rows first.
///
/// <para>Every seeder in this solution reads what is present and then inserts
/// what is missing, and the whole sequence runs on EVERY API instance at
/// startup without a lease around it. On the first boot of a fresh database
/// several instances therefore read the same empty tables and insert the same
/// rows, and the unique index rejects whichever one arrives second. Left
/// unhandled that rejection propagates out of the host's seed block, so the
/// losing instance fails to start over work that had in fact succeeded.</para>
///
/// <para>The rejection is only good news for a seeder: the rows it exists to
/// guarantee are now present. So the losing node detaches what the store
/// refused, records that it lost, and carries on booting.</para>
///
/// <para>The tolerance is deliberately narrow. Swallowing a general
/// <see cref="DbUpdateException"/> would report a silent success for a genuine
/// write failure, which is a far worse outcome than a node that refuses to
/// start: an operator would be left with a half-seeded database and nothing in
/// the log to say so. Only the two SQL Server uniqueness errors are treated as
/// a lost race; everything else still propagates.</para>
/// </summary>
internal static class SeedRaceGuard
{
    /// <summary>
    /// Persists a seeder's pending inserts, tolerating a lost first-boot race.
    /// Returns true when this instance wrote the rows and false when another
    /// instance had already written them, so the caller can keep its "seeded N
    /// rows" log line honest.
    /// </summary>
    /// <param name="context">The context holding the pending inserts.</param>
    /// <param name="logger">The calling seeder's logger.</param>
    /// <param name="what">
    /// A short human description of the batch, e.g. "Baseline interests seed".
    /// It names the losing step in the log so an operator reading a startup log
    /// can tell which seed step deferred to another instance.
    /// </param>
    /// <param name="cancellationToken">Cancels the save.</param>
    public static async Task<bool> SaveToleratingFirstBootRaceAsync(
        this DbContext context,
        ILogger logger,
        string what,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueIndexViolation(exception))
        {
            DetachAddedEntries(context);
            logger.LogInformation(
                "{What} lost the first-boot race — another instance inserted the same rows.",
                what);
            return false;
        }
    }

    /// <summary>
    /// Drops every pending insert on the context.
    ///
    /// <para>Detaching is not housekeeping, it is what lets the boot continue.
    /// A failed <c>SaveChanges</c> leaves the rejected rows tracked as Added, so
    /// the seeder's NEXT save would re-send them and be rejected again — and the
    /// step after that would fail on a genuinely different write it could not
    /// explain. It drops ALL pending inserts rather than a list the caller kept,
    /// because a rejected parent usually drags children in with it (a demo
    /// profile brings its identity-document rows), and a child left tracked
    /// while its parent is gone fails the next save on a foreign key
    /// instead.</para>
    ///
    /// <para>It also clears the row-audit entries the auditing interceptor
    /// attached to the same context on its way into the failed save. Left
    /// tracked, those would ship with the seeder's next save and put an audit
    /// trail in the database for rows this instance never wrote.</para>
    /// </summary>
    public static void DetachAddedEntries(DbContext context)
    {
        // Materialised first: setting State walks the entry off the change
        // tracker, so iterating it lazily would mutate the collection in flight.
        var pendingInserts = context.ChangeTracker.Entries()
            .Where(entry => entry.State == EntityState.Added)
            .ToList();
        foreach (var entry in pendingInserts)
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>True only when the store rejected the write on a UNIQUE index —
    /// SQL Server 2601 (duplicate key in a unique index) or 2627 (unique
    /// constraint). Every other <see cref="DbUpdateException"/> must propagate;
    /// swallowing them would hide a real seed failure behind a silent
    /// success.</summary>
    public static bool IsUniqueIndexViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
