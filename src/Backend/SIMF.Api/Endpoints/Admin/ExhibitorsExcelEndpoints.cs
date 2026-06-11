// Tests: SIMF.Api.Tests/ExhibitorsExcelTests.cs
using FastEndpoints;
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.Exhibitors.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Exhibitors;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/exhibitors/export</c> — the D-356 grid export for the
/// exhibitors module. All the work lives in <see cref="AdminGridExportEndpoint{TRow}"/>;
/// this subclass only declares the route, permission, sheet/file names, the
/// column layout (the grid's visible columns plus the contact fields the
/// summary already carries), and how to list + identify an exhibitor row via
/// the same <see cref="IAdminExhibitorService"/> the list endpoint uses.
/// </summary>
public sealed class ExportExhibitorsEndpoint(IAdminExhibitorService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminExhibitorSummary>(exporter)
{
    protected override string RoutePath => "/admin/exhibitors/export";
    protected override string Permission => PermissionCatalog.Exhibitors.Export;
    protected override string SheetName => "Exhibitors";
    protected override string FilePrefix => "simf-exhibitors";

    protected override IReadOnlyList<GridExcelColumn<AdminExhibitorSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminExhibitorSummary>> _columns =
    [
        new("NameEn", row => row.NameEn),
        new("NameAr", row => row.NameAr),
        new("ContactEmail", row => row.ContactEmail),
        new("ContactPhone", row => row.ContactPhone),
        new("Website", row => row.Website),
        new("AccountCount", row => row.AccountCount),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminExhibitorSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Items;

    protected override Guid IdOf(AdminExhibitorSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/exhibitors/import</c> — the D-356 grid import
/// (insert-only). The base does the upload defence, parse and per-row error
/// aggregation; this subclass binds one row to <see cref="CreateExhibitorRequest"/>
/// and creates it (the service rejects an invalid name → a per-row error, not a
/// batch abort).
/// <para>
/// Intentionally omitted from import: <c>AccountCount</c> (read-only derived
/// count), <c>IsActive</c> (a created exhibitor is always active), and the
/// <c>ContactId</c> directory FK — a cross-record GUID cannot be expressed as
/// plain spreadsheet text, so a contact link is set later from the editor.
/// </para>
/// </summary>
public sealed class ImportExhibitorsEndpoint(IAdminExhibitorService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/exhibitors/import";
    protected override string Permission => PermissionCatalog.Exhibitors.Import;
    protected override string SheetName => "Exhibitors";
    protected override IReadOnlyList<string> RequiredHeaders => ["NameEn", "NameAr"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("NameEn", out var name) ? name : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var nameEn = row.Cells.GetValueOrDefault("NameEn", string.Empty);
        var nameAr = row.Cells.GetValueOrDefault("NameAr", string.Empty);
        if (string.IsNullOrWhiteSpace(nameEn) || string.IsNullOrWhiteSpace(nameAr))
        {
            throw new DataValidationException(
                "Both the English and Arabic names are required.",
                "الاسمان بالإنجليزية والعربية كلاهما مطلوبان.");
        }

        await service.CreateAsync(actorId, new CreateExhibitorRequest
        {
            NameEn = nameEn,
            NameAr = nameAr,
            ContactEmail = NullIfBlank(row.Cells.GetValueOrDefault("ContactEmail", string.Empty)),
            ContactPhone = NullIfBlank(row.Cells.GetValueOrDefault("ContactPhone", string.Empty)),
            Website = NullIfBlank(row.Cells.GetValueOrDefault("Website", string.Empty)),
        }, ct);
        return GridRowApplyKind.Created;
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
