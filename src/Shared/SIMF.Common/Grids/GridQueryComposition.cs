// Tests: SIMF.Api.Tests/GridColumnsTests.cs
using System.Linq.Expressions;

namespace SIMF.Common.Grids;

/// <summary>
/// Composes a <see cref="GridQuery"/> onto a queryable: validate, filter, search,
/// order, tiebreak. Everything up to, and deliberately not including, paging.
///
/// <para>
/// Paging is split off into the Infrastructure-side <c>ToGridPageAsync</c> for two
/// reasons. The hard one: this project references no EF Core, so <c>CountAsync</c>
/// and <c>ToListAsync</c> cannot be called from here. The good one: an Excel export
/// needs the same filters, the same search and the same order over the whole result
/// set with no <c>Skip</c>/<c>Take</c>, so if composition and paging were fused,
/// every export would silently lose filter parity with the grid it came from.
/// </para>
/// </summary>
public static class GridQueryComposition
{
    /// <summary>The longest accepted free-text search term. An 8 KB term would
    /// otherwise become an 8 KB substring test per searchable column per row.</summary>
    private const int MaxSearchLength = 128;

    /// <summary>The most filter keys one request may carry. 500 valid keys would
    /// otherwise become 500 WHERE clauses and 500 compiled-query cache entries.</summary>
    private const int MaxFilterKeys = 20;

    /// <summary>
    /// Applies the query's filters, search and ordering. The ordering always ends
    /// in <paramref name="tiebreak"/>, which must be unique per row: a non-unique
    /// ORDER BY lets SQL Server return rows in any order among ties, so the same row
    /// can appear on two pages while another appears on none. Making the tiebreak a
    /// required parameter is the only way to make that mistake unexpressible.
    /// </summary>
    /// <param name="source">The entity set. Do not pre-page it: a <c>Take</c>
    /// already on the source makes EF push the ordering into a subquery instead of
    /// clearing it for the count. Do pre-filter it for scope (an owning parent, a
    /// role restriction) — those predicates compose ahead of the grid's.</param>
    /// <param name="query">The request.</param>
    /// <param name="columns">The resource's column declaration, a static field.</param>
    /// <param name="tiebreak">A per-row unique key, normally the primary key.</param>
    /// <exception cref="ApiException">400 when the request names a column that is not
    /// declared, sends the same column twice, or carries a value that will not
    /// parse.</exception>
    public static IQueryable<TEntity> ApplyGrid<TEntity, TKey>(
        this IQueryable<TEntity> source,
        GridQuery query,
        GridColumns<TEntity> columns,
        Expression<Func<TEntity, TKey>> tiebreak)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(tiebreak);

        columns.Seal();

        // 1. VALIDATE. Every key is resolved and every predicate built before a
        // single operator is composed, so a bad request never half-applies.
        //
        // The keys are walked in a canonical order, and a case-variant duplicate is
        // rejected, for two reasons. Ordering: each filter becomes a separate Where,
        // so {code, name} and {name, code} build structurally different trees and
        // take two slots in EF's compiled-query cache; a canonical order collapses
        // them to one. Duplicates: GridQuery.Filters is a case-SENSITIVE dictionary
        // but columns are matched case-insensitively, so "isActive" and "IsActive"
        // both bind to the same column and AND to the empty set, which reads as an
        // empty grid with no explanation.
        if (query.Filters.Count > MaxFilterKeys)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                $"A list request may carry at most {MaxFilterKeys} filter columns.",
                $"لا يمكن أن يحتوي طلب القائمة على أكثر من {MaxFilterKeys} عمود تصفية.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var predicates = new List<Expression<Func<TEntity, bool>>>(query.Filters.Count);

        foreach (var (key, raw) in query.Filters.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            // The key is checked even when the value is blank. A stale key is a
            // client bug whether or not it carries a value today, and the grid
            // removes empty entries anyway.
            var filter = columns.RequireFilter(key);

            if (!seen.Add(key))
            {
                throw new ApiException(
                    ErrorCodes.GridFilterKeyInvalid, 400,
                    $"The filter column '{key}' was sent more than once.",
                    $"تم إرسال عمود التصفية '{key}' أكثر من مرة.",
                    [new ApiErrorDetail
                    {
                        Field = key,
                        Message = $"Duplicate filter column '{key}'.",
                        MessageArabic = $"عمود تصفية مكرر '{key}'.",
                    }]);
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            predicates.Add(filter(raw.Trim()));
        }

        var search = BuildSearch(columns, query.Search);

        var sortColumn = string.IsNullOrWhiteSpace(query.Sort)
            ? null
            : columns.RequireSortColumn(query.Sort.Trim());

        // 2. FILTER. ANDed, one Where per column, all server-side.
        var rows = source;
        foreach (var predicate in predicates)
        {
            rows = rows.Where(predicate);
        }

        // 3. SEARCH. ORed across the searchable columns, still server-side.
        if (search is not null)
        {
            rows = rows.Where(search);
        }

        // 4. ORDER plus the mandatory tiebreak.
        return Order(rows, columns, sortColumn, query.SortDescending, tiebreak);
    }

    private static Expression<Func<TEntity, bool>>? BuildSearch<TEntity>(
        GridColumns<TEntity> columns, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        if (search.Length > MaxSearchLength)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                $"A search term may be at most {MaxSearchLength} characters.",
                $"يجب ألا يزيد نص البحث عن {MaxSearchLength} حرفًا.");
        }

        var predicate = GridSearchPredicate.Build(columns.SearchSelectors, search);

        // "No predicate but a term was sent" means the resource declared no
        // searchable column. That is the same failure as a skipped filter: the
        // search box returns the unfiltered set, which on a people grid looks like a
        // result rather than a fault. So it is a 400, not a no-op.
        return predicate ?? throw new ApiException(
            ErrorCodes.GridSearchNotSupported, 400,
            "This list has no searchable columns, so it cannot be searched.",
            "لا تحتوي هذه القائمة على أعمدة قابلة للبحث، لذا لا يمكن البحث فيها.");
    }

    private static IQueryable<TEntity> Order<TEntity, TKey>(
        IQueryable<TEntity> rows,
        GridColumns<TEntity> columns,
        GridColumn<TEntity>? sortColumn,
        bool descending,
        Expression<Func<TEntity, TKey>> tiebreak)
    {
        if (sortColumn?.OrderBy is not null)
        {
            return sortColumn.OrderBy(rows, descending).ThenBy(tiebreak);
        }

        IOrderedQueryable<TEntity>? ordered = null;
        foreach (var (column, levelDescending) in columns.DefaultOrderLevels)
        {
            ordered = ordered is null
                ? column.OrderBy!(rows, levelDescending)
                : column.ThenBy!(ordered, levelDescending);
        }

        return ordered is null
            ? rows.OrderBy(tiebreak)
            : ordered.ThenBy(tiebreak);
    }
}
