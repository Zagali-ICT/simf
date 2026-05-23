namespace SIMF.Common;

/// <summary>
/// The query shape every server-paged list endpoint accepts (decision D-044).
/// Mirrors the IBS-V10 <c>GetListData(Top, Next, Query, Filter)</c> shape so
/// the rest of the SIMF endpoints can use one client-side primitive
/// (<c>SimfDataGrid</c>) for every list page.
///
/// <para>
/// All four members are optional. A request with the defaults returns the
/// first page of the natural order. The endpoint is free to clamp <c>Top</c>
/// to a maximum (today: 200 rows, the default page size is 20).
/// </para>
/// </summary>
public sealed class GridQuery
{
    /// <summary>The number of rows to skip; the offset of the first row. Default 0.</summary>
    public int Skip { get; set; }

    /// <summary>The page size — how many rows to return. Default 20, clamped to 200.</summary>
    public int Top { get; set; } = 20;

    /// <summary>
    /// The case-insensitive free-text search applied across the
    /// endpoint-specific "searchable" columns. Empty / null means no search.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// The column key to sort by. The endpoint validates the key against the
    /// allowed list. Empty / null means the endpoint's natural order.
    /// </summary>
    public string? Sort { get; set; }

    /// <summary>When true, the sort is descending. Default false.</summary>
    public bool SortDescending { get; set; }

    /// <summary>
    /// Per-column filter values keyed by the column key. Empty values are
    /// ignored. The endpoint validates the keys.
    /// </summary>
    public Dictionary<string, string> Filters { get; set; } = new();
}

/// <summary>
/// One page of a server-paged list. The endpoint returns this wrapped in
/// <see cref="ApiResult{T}"/> so a client uses one parser for every list.
/// </summary>
/// <typeparam name="TItem">The row type.</typeparam>
public sealed class GridPage<TItem>
{
    /// <summary>The rows on this page.</summary>
    public IReadOnlyList<TItem> Items { get; init; } = Array.Empty<TItem>();

    /// <summary>The total row count across every page, after filters.</summary>
    public int Total { get; init; }

    /// <summary>The <see cref="GridQuery.Skip"/> echoed for clients that
    /// rebuild the next request from the response.</summary>
    public int Skip { get; init; }

    /// <summary>The <see cref="GridQuery.Top"/> echoed.</summary>
    public int Top { get; init; }

    /// <summary>Builds a page from materialised rows and a known total.</summary>
    public static GridPage<TItem> Of(
        IReadOnlyList<TItem> items, int total, GridQuery query) =>
        new()
        {
            Items = items,
            Total = total,
            Skip = query.Skip,
            Top = query.Top,
        };
}
