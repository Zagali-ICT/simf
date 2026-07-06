// Tests: SIMF.Common.GridExportPaging.CollectAllAsync — the D-642 export pager.
using SIMF.Common;
using Xunit;

namespace SIMF.Application.Tests;

/// <summary>
/// Unit tests for <see cref="GridExportPaging.CollectAllAsync{T}"/> — the paging
/// walk that fixes the D-642 export truncation. It must collect every page (each
/// list service clamps a page to its own size), stop on an empty page, and never
/// return more than the export cap.
/// </summary>
public class GridExportPagingTests
{
    // A fake list whose page size mimics a service's ClampPage cap.
    private static Func<int, Task<IReadOnlyList<int>>> Paged(IReadOnlyList<int> data, int pageSize) =>
        skip => Task.FromResult<IReadOnlyList<int>>(data.Skip(skip).Take(pageSize).ToList());

    [Fact]
    public async Task Collects_every_row_across_multiple_pages()
    {
        // 250 rows served 200-at-a-time — the exact shape that used to truncate.
        var data = Enumerable.Range(0, 250).ToList();

        var rows = await GridExportPaging.CollectAllAsync(Paged(data, 200), cap: 5_000);

        Assert.Equal(250, rows.Count);
        Assert.Equal(data, rows);
    }

    [Fact]
    public async Task Stops_at_the_cap_when_more_rows_remain()
    {
        var data = Enumerable.Range(0, 10_000).ToList();

        var rows = await GridExportPaging.CollectAllAsync(Paged(data, 200), cap: 5_000);

        Assert.Equal(5_000, rows.Count);
    }

    [Fact]
    public async Task Trims_a_final_page_that_overshoots_the_cap()
    {
        // cap is not a multiple of the page size — the last page overshoots the cap
        // and must be trimmed back to it.
        var data = Enumerable.Range(0, 10_000).ToList();

        var rows = await GridExportPaging.CollectAllAsync(Paged(data, 300), cap: 4_900);

        Assert.Equal(4_900, rows.Count);
    }

    [Fact]
    public async Task Empty_source_returns_no_rows_in_a_single_call()
    {
        var calls = 0;

        var rows = await GridExportPaging.CollectAllAsync(
            skip =>
            {
                calls++;
                return Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
            },
            cap: 5_000);

        Assert.Empty(rows);
        Assert.Equal(1, calls); // stops immediately, no needless second call
    }

    [Fact]
    public async Task A_single_short_page_is_returned_whole()
    {
        var data = Enumerable.Range(0, 42).ToList();

        var rows = await GridExportPaging.CollectAllAsync(Paged(data, 200), cap: 5_000);

        Assert.Equal(42, rows.Count);
    }
}
