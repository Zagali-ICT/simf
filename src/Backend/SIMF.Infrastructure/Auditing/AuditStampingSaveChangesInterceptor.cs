using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SIMF.Application.Abstractions;
using SIMF.Domain.Common;

namespace SIMF.Infrastructure.Auditing;

/// <summary>
/// Stamps the audit columns on every <see cref="BaseAuditEntity"/> written
/// through the App DbContext: <c>CreatedAt</c>/<c>CreatedBy</c> on INSERT and
/// <c>UpdatedAt</c>/<c>UpdatedBy</c> on UPDATE, from the signed-in actor
/// (<see cref="IRequestContext.ActorUserId"/> — the JWT <c>sub</c> claim) and the
/// shared <see cref="TimeProvider"/>.
///
/// <para>This is the single writer for the actor/time audit stamp, so no service
/// has to remember to set <c>CreatedBy</c>/<c>UpdatedBy</c> by hand. <c>CreatedAt</c>
/// and <c>CreatedBy</c> are only filled when left unset, so explicit seed / import
/// values are preserved; <c>UpdatedAt</c>/<c>UpdatedBy</c> are always refreshed on a
/// modification (that is the column's purpose).</para>
///
/// <para>Registered on the App DbContext only (Identity entities are standalone
/// and frozen — they carry no audit base), and BEFORE the
/// <see cref="RowAuditingSaveChangesInterceptor"/> so the stamped values are
/// captured in the row-audit trail rather than recorded as still-empty.</para>
/// </summary>
internal sealed class AuditStampingSaveChangesInterceptor(
    IRequestContext requestContext,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
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

        var now = timeProvider.GetUtcNow();
        var actorUserId = requestContext.ActorUserId;

        foreach (var entry in context.ChangeTracker.Entries<BaseAuditEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.CreatedAt == default)
                    {
                        entry.Entity.CreatedAt = now;
                    }
                    if (entry.Entity.CreatedBy == Guid.Empty)
                    {
                        entry.Entity.CreatedBy = actorUserId ?? Guid.Empty;
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = actorUserId;
                    break;
            }
        }
    }
}
