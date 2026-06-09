// Tests: SIMF.Api.Tests/SessionCategoriesExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.IdentityAccess;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/session-categories/export</c> — D-356 grid export for
/// the dynamic session-category lookup. All the work lives in
/// <see cref="AdminGridExportEndpoint{TRow}"/>; this subclass only declares the
/// route, permission, sheet/file names, the column layout, and how to list +
/// identify a category row. Mirrors the Interests reference vertical.
/// </summary>
public sealed class ExportSessionCategoriesEndpoint(
    IAdminSessionCategoryService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminSessionCategorySummary>(exporter)
{
    protected override string RoutePath => "/admin/session-categories/export";
    protected override string Permission => PermissionCatalog.SessionCategories.Export;
    protected override string SheetName => "SessionCategories";
    protected override string FilePrefix => "simf-session-categories";

    protected override IReadOnlyList<GridExcelColumn<AdminSessionCategorySummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminSessionCategorySummary>> _columns =
    [
        new("Name", row => row.Name),
        new("NameArabic", row => row.NameArabic),
        new("DisplayOrder", row => row.DisplayOrder),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminSessionCategorySummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAsync(query, ct)).Items;

    protected override Guid IdOf(AdminSessionCategorySummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/session-categories/import</c> — D-356 grid import
/// (insert-only). The base does the upload defence, parse and per-row error
/// aggregation; this subclass binds one row to
/// <see cref="AdminCreateSessionCategoryRequest"/> and creates it (the service
/// validates each name → a per-row error, not a batch abort).
/// </summary>
public sealed class ImportSessionCategoriesEndpoint(
    IAdminSessionCategoryService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/session-categories/import";
    protected override string Permission => PermissionCatalog.SessionCategories.Import;
    protected override string SheetName => "SessionCategories";
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

        await service.CreateAsync(actorId, new AdminCreateSessionCategoryRequest
        {
            Name = name,
            NameArabic = row.Cells.GetValueOrDefault("NameArabic", string.Empty),
            DisplayOrder = int.TryParse(
                row.Cells.GetValueOrDefault("DisplayOrder", string.Empty), out var order) ? order : 0,
        }, ct);
        return GridRowApplyKind.Created;
    }
}
