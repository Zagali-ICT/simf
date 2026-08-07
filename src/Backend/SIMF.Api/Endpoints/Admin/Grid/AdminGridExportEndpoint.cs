using FastEndpoints;
using SIMF.Application.Excel;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin.Grid;

/// <summary>
/// Generic base for a resource's <c>POST /admin/{resource}/export</c> endpoint.
/// A concrete subclass supplies the route, the <c>{Resource}.Export</c>
/// permission, the sheet name + file prefix, the column descriptors, and how to
/// list + identify its rows; this base owns the auth gate, the ids-or-query
/// selection (capped), the workbook render and the binary response. Mirrors the
/// proven <c>ExportUsersEndpoint</c> so every grid export behaves the same.
/// </summary>
/// <typeparam name="TRow">The grid summary row type.</typeparam>
public abstract class AdminGridExportEndpoint<TRow>(IGridExcelExporter exporter)
    : Endpoint<AdminGridExportRequest>
{
    /// <summary>The whole-grid export is capped at this many rows.</summary>
    protected const int MaxExportRows = 5_000;

    /// <summary>The endpoint route, e.g. <c>/admin/interests/export</c>.</summary>
    protected abstract string RoutePath { get; }

    /// <summary>The <c>{Resource}.Export</c> permission code that gates this endpoint.</summary>
    protected abstract string Permission { get; }

    /// <summary>The worksheet name (also required, by exact name, on import).</summary>
    protected abstract string SheetName { get; }

    /// <summary>The downloaded file-name prefix, e.g. <c>simf-interests</c>.</summary>
    protected abstract string FilePrefix { get; }

    /// <summary>The export column layout (header + per-row value selector).</summary>
    protected abstract IReadOnlyList<GridExcelColumn<TRow>> Columns { get; }

    /// <summary>Lists one clamped page of rows matching <paramref name="query"/>.
    /// The base calls this once per page (advancing <see cref="GridQuery.Skip"/>)
    /// and stops when a page is empty or <see cref="MaxExportRows"/> is reached, so
    /// the list service keeps its own page-size clamp untouched.</summary>
    protected abstract Task<IReadOnlyList<TRow>> ListAsync(GridQuery query, CancellationToken ct);

    /// <summary>The row's id, used to honour a selected-ids export.</summary>
    protected abstract Guid IdOf(TRow row);

    public override void Configure()
    {
        Post(RoutePath);
        Policies(PermissionCatalog.PolicyFor(Permission), nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Admin");
        Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"));
        Summary(summary => summary.Summary =
            "Export the grid to an XLSX workbook (selected rows, or the whole filtered set).");
    }

    public override async Task HandleAsync(AdminGridExportRequest req, CancellationToken ct)
    {
        var source = req.Query ?? new GridQuery();
        // Page through the resource's normal list (each service clamps Top to its
        // own page size) until the whole filtered set is collected or MaxExportRows
        // is reached — an export used to truncate at the first page. The list
        // contract is untouched, so there is no client-reachable escape hatch on
        // the shared GridQuery. This also fixes the selected-ids path: an id beyond
        // the first page is now found because every page is fetched.
        var rows = await GridExportPaging.CollectAllAsync(
            skip => ListAsync(GridExportPaging.Page(source, skip, MaxExportRows), ct), MaxExportRows);

        if (req.Ids is { Count: > 0 })
        {
            var wanted = req.Ids.ToHashSet();
            rows = rows.Where(row => wanted.Contains(IdOf(row))).ToList();
        }

        var bytes = exporter.Export(rows, Columns, SheetName);
        HttpContext.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{FilePrefix}-{SimfClock.Now:yyyyMMddHHmmss}.xlsx\"";
        await Send.BytesAsync(bytes,
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            cancellation: ct);
    }
}
