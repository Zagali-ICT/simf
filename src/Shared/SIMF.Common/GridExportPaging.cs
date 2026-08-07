namespace SIMF.Common;

/// <summary>
/// Collects a whole grid result set for an Excel export by walking the resource's
/// normal server-paged list one page at a time.
///
/// <para>
/// Every list service clamps <c>Top</c> to its own page size (see
/// <see cref="GridQuery.ClampPage"/>), so a single oversized <c>Top</c> is
/// silently truncated to one page. Rather than widen that clamp — which
/// would require a client-reachable escape hatch on the shared, model-bound
/// <see cref="GridQuery"/> and so weaken the list-endpoint DoS cap — the export
/// keeps fetching successive pages until the set is exhausted or the export cap is
/// reached. The list contract stays exactly as-is; only the export pages through.
/// </para>
/// </summary>
public static class GridExportPaging
{
    /// <summary>
    /// Repeatedly invokes <paramref name="fetchPage"/> with the running row count
    /// as the skip offset, accumulating each already-clamped page until a page
    /// comes back empty or <paramref name="cap"/> rows have been collected. The
    /// result is trimmed to at most <paramref name="cap"/> rows.
    /// </summary>
    /// <param name="fetchPage">Fetches one clamped page starting at the given skip
    /// offset. An empty page signals the end of the set. Correctness depends on the
    /// fetch honouring the skip offset over a stable order — every SIMF list service
    /// does (it applies <see cref="GridQuery.ClampPage"/> then <c>.Skip(skip)</c>).</param>
    /// <param name="cap">The maximum number of rows to collect (the export bound,
    /// so an "export everything" can never load an unbounded set into memory).</param>
    public static async Task<IReadOnlyList<T>> CollectAllAsync<T>(
        Func<int, Task<IReadOnlyList<T>>> fetchPage, int cap)
    {
        var all = new List<T>();
        while (all.Count < cap)
        {
            var batch = await fetchPage(all.Count);
            if (batch.Count == 0)
            {
                break;
            }
            all.AddRange(batch);
        }
        return all.Count > cap ? all.GetRange(0, cap) : all;
    }

    /// <summary>
    /// Builds one export page query from the caller's query: its filters / search /
    /// sort preserved, <see cref="GridQuery.Skip"/> set to <paramref name="skip"/>
    /// and <see cref="GridQuery.Top"/> to <paramref name="top"/> (the list service
    /// clamps that back to its own page size). A fresh instance every page, so the
    /// caller's own query is never mutated in place.
    /// </summary>
    public static GridQuery Page(GridQuery source, int skip, int top) => new()
    {
        Skip = skip,
        Top = top,
        Search = source.Search,
        Sort = source.Sort,
        SortDescending = source.SortDescending,
        Filters = new Dictionary<string, string>(source.Filters),
    };
}
