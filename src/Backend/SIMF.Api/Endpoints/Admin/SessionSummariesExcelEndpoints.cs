// Tests: SIMF.Api.Tests/SessionSummariesExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/session-summaries/export</c> — the grid export
/// for the Scientific-Committee session-summary / محضر desk.
/// <b>Export only:</b> the desk drafts / edits / publishes summaries through its
/// own bespoke endpoints, so no generic import path is added here. Rows are the
/// same <see cref="AdminSessionSummaryRow"/> the desk lists, selected by the
/// row's <c>SessionId</c>; the boolean status / source flags are resolved to
/// readable text. Gated by <see cref="PermissionCatalog.SessionSummaries.Export"/>.
/// </summary>
public sealed class ExportSessionSummariesEndpoint(
    IAdminSessionSummaryService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminSessionSummaryRow>(exporter)
{
    protected override string RoutePath => "/admin/session-summaries/export";
    protected override string Permission => PermissionCatalog.SessionSummaries.Export;
    protected override string SheetName => "SessionSummaries";
    protected override string FilePrefix => "simf-session-summaries";

    protected override IReadOnlyList<GridExcelColumn<AdminSessionSummaryRow>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminSessionSummaryRow>> _columns =
    [
        new("SessionCode", row => row.SessionCode),
        new("SessionTitle", row => row.SessionTitle),
        new("SessionTitleArabic", row => row.SessionTitleArabic),
        new("SessionStart", row => row.SessionStart),
        new("Status", row => StatusText(row)),
        new("Source", row => SourceText(row)),
        new("PublishedAt", row => row.PublishedAt),
        new("UpdatedAt", row => row.UpdatedAt),
    ];

    // Mirror the desk's Status column: no summary → "None"; published → "Published";
    // otherwise a draft awaiting publish.
    private static string StatusText(AdminSessionSummaryRow row) =>
        !row.HasSummary ? "None"
        : row.IsPublished ? "Published"
        : "Draft";

    // Mirror the desk's Source column: AI-drafted vs. hand-written (— when no
    // summary exists yet).
    private static string SourceText(AdminSessionSummaryRow row) =>
        !row.HasSummary ? "—"
        : row.GeneratedByAi ? "AI"
        : "Manual";

    // The desk list endpoint reads every active session in one query (no grid
    // query); the export base still caps + applies the selected-ids filter.
    protected override async Task<IReadOnlyList<AdminSessionSummaryRow>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        await service.ListAsync(ct);

    protected override Guid IdOf(AdminSessionSummaryRow row) => row.SessionId;
}
