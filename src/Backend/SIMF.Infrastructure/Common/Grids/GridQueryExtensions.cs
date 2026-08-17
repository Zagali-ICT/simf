// Tests: SIMF.Api.Tests/GridColumnsTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SIMF.Common;
using SIMF.Common.Grids;

namespace SIMF.Infrastructure.Common.Grids;

/// <summary>
/// The one function every Control Panel list service calls. It runs the whole fixed
/// pipeline — validate, filter, search, order plus tiebreak, count, page, project —
/// and hands back the finished page.
///
/// <para>
/// It lives here rather than beside <see cref="GridQueryComposition"/> because
/// <c>CountAsync</c> / <c>ToListAsync</c> / <c>AsNoTracking</c> need
/// <c>Microsoft.EntityFrameworkCore</c>, and <c>SIMF.Common</c> has no package
/// references at all — it is referenced by the Control Panel, the Website and the
/// API client, none of which should drag in the ORM.
/// </para>
/// </summary>
public static class GridQueryExtensions
{
    /// <summary>
    /// Returns one page of <typeparamref name="TRow"/>.
    /// </summary>
    /// <param name="source">The entity set, optionally pre-filtered for scope.</param>
    /// <param name="query">The request. Unknown sort or filter keys 400 here.</param>
    /// <param name="columns">The resource's column declaration, a static field. It
    /// also carries the page-size policy.</param>
    /// <param name="tiebreak">A per-row unique key. Required, so an unstable page
    /// order cannot be expressed.</param>
    /// <param name="projection">The DTO projection. Passed in so the SELECT pulls
    /// only the columns the row needs, and so it may project computed values that
    /// are not sortable or filterable columns at all, such as
    /// <c>gate.Assignments.Count(assignment =&gt; assignment.IsActive)</c>. Any
    /// <c>Include</c> on the source is inert under a projection and should be
    /// deleted rather than carried over.</param>
    /// <param name="cancellationToken">Cancels both round trips.</param>
    public static async Task<GridPage<TRow>> ToGridPageAsync<TEntity, TRow, TKey>(
        this IQueryable<TEntity> source,
        GridQuery query,
        GridColumns<TEntity> columns,
        Expression<Func<TEntity, TKey>> tiebreak,
        Expression<Func<TEntity, TRow>> projection,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(projection);

        // AsNoTracking is applied here rather than left to the caller. A grid page is
        // a pure read by definition, and every call site is another chance to forget
        // it. It is idempotent when the caller already applied it; the only choice it
        // overrides is AsNoTrackingWithIdentityResolution, which is meaningless under
        // a DTO projection.
        var rows = source.AsNoTracking().ApplyGrid(query, columns, tiebreak);

        var (skip, top) = query.ClampPage(columns.FallbackTop, columns.MaxTop);

        // The total is the size of the FILTERED set, so it is counted before
        // Skip/Take and on the server. Counting after paging would report the page
        // size; counting client-side would fetch every row to discard it. EF strips
        // the ORDER BY when translating an aggregate, so the ordering already
        // composed above costs nothing here.
        var total = await rows.CountAsync(cancellationToken);

        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(projection)
            .ToListAsync(cancellationToken);

        return GridPage<TRow>.Of(page, total, skip, top);
    }
}
