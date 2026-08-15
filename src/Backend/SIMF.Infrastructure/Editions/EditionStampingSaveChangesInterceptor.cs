using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SIMF.Domain.Profiles;

namespace SIMF.Infrastructure.Editions;

/// <summary>
/// Stamps <see cref="UserProfile.EditionYear"/> on every attendee record as it
/// is inserted, from the year currently open.
///
/// <para>Written as an interceptor for the same reason the audit stamp is: there
/// are a dozen places that create an attendee — self sign-up, the walk-in desk,
/// a bulk order, the exhibition desk, approval, the seeder — and a stamp that
/// each of them has to remember is a stamp that the thirteenth will not carry.
/// The consequence of missing it is not cosmetic: an unstamped record reads as
/// year zero, which the gate deliberately admits, so the omission would be
/// invisible until someone audited why a badge from a closed year still
/// worked.</para>
///
/// <para>Only ever fills a zero. An explicit value — a seed, an import, a test
/// building last year's attendee on purpose — is left exactly as written.</para>
/// </summary>
internal sealed class EditionStampingSaveChangesInterceptor(
    IEventEditionCache cache) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null) { return; }

        // The cache is populated by the first read of the open year, which every
        // gate scan and the admin screen both do. Until something has read it,
        // there is nothing to stamp with, and leaving the column at zero is the
        // safe direction: the gate admits a zero rather than refusing a badge
        // over a value the server never knew.
        if (!cache.TryGet(out var year)) { return; }

        foreach (var entry in context.ChangeTracker.Entries<UserProfile>())
        {
            if (entry.State == EntityState.Added && entry.Entity.EditionYear == 0)
            {
                entry.Entity.EditionYear = year;
            }
        }
    }
}
