// Tests: SIMF.Api.Tests/ThemesExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/themes/export</c> — the D-356 grid export for
/// programme themes. All the work lives in
/// <see cref="AdminGridExportEndpoint{TRow}"/>; this subclass only declares the
/// route, permission, sheet/file names, the column layout (mirroring the
/// Themes grid), and how to list + identify a theme row.
/// </summary>
public sealed class ExportThemesEndpoint(IAdminThemeService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminThemeSummary>(exporter)
{
    protected override string RoutePath => "/admin/themes/export";
    protected override string Permission => PermissionCatalog.Themes.Export;
    protected override string SheetName => "Themes";
    protected override string FilePrefix => "simf-themes";

    protected override IReadOnlyList<GridExcelColumn<AdminThemeSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminThemeSummary>> _columns =
    [
        new("Code", row => row.Code),
        new("Name", row => row.Name),
        new("NameArabic", row => row.NameArabic),
        new("DisplayOrder", row => row.DisplayOrder),
        new("PageColor", row => row.PageColor),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminThemeSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Items;

    protected override Guid IdOf(AdminThemeSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/themes/import</c> — the D-356 grid import (insert-only)
/// for programme themes. The base does the upload defence, parse and per-row
/// error aggregation; this subclass binds one row to
/// <see cref="AdminCreateThemeRequest"/> and creates it (the service rejects a
/// duplicate code → a per-row error, not a batch abort).
/// </summary>
public sealed class ImportThemesEndpoint(IAdminThemeService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/themes/import";
    protected override string Permission => PermissionCatalog.Themes.Import;
    protected override string SheetName => "Themes";
    protected override IReadOnlyList<string> RequiredHeaders => ["Code", "Name", "NameArabic", "PageColor"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("Code", out var code) ? code : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var code = row.Cells.GetValueOrDefault("Code", string.Empty);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DataValidationException(
                "The code is required.",
                "الرمز مطلوب.");
        }

        var name = row.Cells.GetValueOrDefault("Name", string.Empty);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DataValidationException(
                "The English name is required.",
                "الاسم بالإنجليزية مطلوب.");
        }

        var nameArabic = row.Cells.GetValueOrDefault("NameArabic", string.Empty);
        if (string.IsNullOrWhiteSpace(nameArabic))
        {
            throw new DataValidationException(
                "The Arabic name is required.",
                "الاسم بالعربية مطلوب.");
        }

        var pageColor = row.Cells.GetValueOrDefault("PageColor", string.Empty);
        if (string.IsNullOrWhiteSpace(pageColor))
        {
            throw new DataValidationException(
                "The page color is required.",
                "لون الصفحة مطلوب.");
        }

        await service.CreateAsync(actorId, new AdminCreateThemeRequest
        {
            Code = code,
            Name = name,
            NameArabic = nameArabic,
            Description = NullIfBlank(row.Cells.GetValueOrDefault("Description", string.Empty)),
            DescriptionArabic = NullIfBlank(row.Cells.GetValueOrDefault("DescriptionArabic", string.Empty)),
            DisplayOrder = int.TryParse(
                row.Cells.GetValueOrDefault("DisplayOrder", string.Empty), out var order) ? order : 0,
            PageColor = pageColor,
        }, ct);
        return GridRowApplyKind.Created;
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
