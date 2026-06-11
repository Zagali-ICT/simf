// Tests: SIMF.Api.Tests/InterestExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/interests/export</c> — the reference D-356 grid export.
/// All the work lives in <see cref="AdminGridExportEndpoint{TRow}"/>; this
/// subclass only declares the route, permission, sheet/file names, the column
/// layout, and how to list + identify an interest row.
/// </summary>
public sealed class ExportInterestsEndpoint(IInterestService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminInterestSummary>(exporter)
{
    protected override string RoutePath => "/admin/interests/export";
    protected override string Permission => PermissionCatalog.Interests.Export;
    protected override string SheetName => "Interests";
    protected override string FilePrefix => "simf-interests";

    protected override IReadOnlyList<GridExcelColumn<AdminInterestSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminInterestSummary>> _columns =
    [
        new("Name", row => row.Name),
        new("NameArabic", row => row.NameArabic),
        new("DisplayOrder", row => row.DisplayOrder),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminInterestSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Items;

    protected override Guid IdOf(AdminInterestSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/interests/import</c> — the reference D-356 grid import
/// (insert-only). The base does the upload defence, parse and per-row error
/// aggregation; this subclass binds one row to <see cref="AdminCreateInterestRequest"/>
/// and creates it (the service rejects duplicates → a per-row error, not a
/// batch abort).
/// </summary>
public sealed class ImportInterestsEndpoint(IInterestService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/interests/import";
    protected override string Permission => PermissionCatalog.Interests.Import;
    protected override string SheetName => "Interests";
    protected override IReadOnlyList<string> RequiredHeaders => ["Name", "NameArabic"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("Name", out var name) ? name : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var name = row.Cells.GetValueOrDefault("Name", string.Empty);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DataValidationException(
                "The English name is required.",
                "الاسم بالإنجليزية مطلوب.");
        }

        await service.CreateAsync(actorId, new AdminCreateInterestRequest
        {
            Name = name,
            NameArabic = row.Cells.GetValueOrDefault("NameArabic", string.Empty),
            DisplayOrder = int.TryParse(
                row.Cells.GetValueOrDefault("DisplayOrder", string.Empty), out var order) ? order : 0,
        }, ct);
        return GridRowApplyKind.Created;
    }
}
