// Tests: SIMF.Api.Tests/BannersExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Cms.Abstractions;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/banners/export</c> — the D-356 grid export for CMS
/// banners. All the work lives in <see cref="AdminGridExportEndpoint{TRow}"/>;
/// this subclass only declares the route, permission, sheet/file names, the
/// column layout (mirroring the Banners grid) and how to list + identify a
/// banner row.
/// </summary>
public sealed class ExportBannersEndpoint(IAdminCmsService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminBannerSummary>(exporter)
{
    protected override string RoutePath => "/admin/banners/export";
    protected override string Permission => PermissionCatalog.Banners.Export;
    protected override string SheetName => "Banners";
    protected override string FilePrefix => "simf-banners";

    protected override IReadOnlyList<GridExcelColumn<AdminBannerSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminBannerSummary>> _columns =
    [
        new("Title", row => row.Title),
        new("TitleArabic", row => row.TitleArabic),
        new("Start", row => row.Start),
        new("End", row => row.End),
        new("DisplayOrder", row => row.DisplayOrder),
        new("IsActive", row => row.IsActive),
        // D-506 — round-trip the bilingual body + image/link (appended so the
        // existing column order is unchanged; import binds by header name and
        // already reads these four columns).
        new("Body", row => row.Body),
        new("BodyArabic", row => row.BodyArabic),
        new("ImageUrl", row => row.ImageUrl),
        new("LinkUrl", row => row.LinkUrl),
    ];

    protected override async Task<IReadOnlyList<AdminBannerSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListBannersAsync(query, ct)).Items;

    protected override Guid IdOf(AdminBannerSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/banners/import</c> — the D-356 grid import (insert-only)
/// for CMS banners. The base does the upload defence, parse and per-row error
/// aggregation; this subclass binds one row to <see cref="CreateBannerRequest"/>
/// and creates it (the service rejects an invalid window/length → a per-row
/// error, not a batch abort). The bilingual body is required to create a banner,
/// so the import workbook carries Body + BodyArabic in addition to the grid's
/// visible columns.
/// </summary>
public sealed class ImportBannersEndpoint(IAdminCmsService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/banners/import";
    protected override string Permission => PermissionCatalog.Banners.Import;
    protected override string SheetName => "Banners";
    protected override IReadOnlyList<string> RequiredHeaders =>
        ["Title", "TitleArabic", "Body", "BodyArabic", "Start", "End"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("Title", out var title) ? title : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var title = row.Cells.GetValueOrDefault("Title", string.Empty);
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DataValidationException(
                "The English title is required.",
                "العنوان بالإنجليزية مطلوب.");
        }

        var titleArabic = row.Cells.GetValueOrDefault("TitleArabic", string.Empty);
        if (string.IsNullOrWhiteSpace(titleArabic))
        {
            throw new DataValidationException(
                "The Arabic title is required.",
                "العنوان بالعربية مطلوب.");
        }

        var body = row.Cells.GetValueOrDefault("Body", string.Empty);
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new DataValidationException(
                "The English body is required.",
                "النص بالإنجليزية مطلوب.");
        }

        var bodyArabic = row.Cells.GetValueOrDefault("BodyArabic", string.Empty);
        if (string.IsNullOrWhiteSpace(bodyArabic))
        {
            throw new DataValidationException(
                "The Arabic body is required.",
                "النص بالعربية مطلوب.");
        }

        var startRaw = row.Cells.GetValueOrDefault("Start", string.Empty);
        if (!DateTime.TryParse(startRaw, out var start))
        {
            throw new DataValidationException(
                "The start date/time is required and must be a valid date.",
                "تاريخ/وقت البداية مطلوب ويجب أن يكون تاريخاً صالحاً.");
        }

        var endRaw = row.Cells.GetValueOrDefault("End", string.Empty);
        if (!DateTime.TryParse(endRaw, out var end))
        {
            throw new DataValidationException(
                "The end date/time is required and must be a valid date.",
                "تاريخ/وقت النهاية مطلوب ويجب أن يكون تاريخاً صالحاً.");
        }

        await service.CreateBannerAsync(actorId, new CreateBannerRequest
        {
            Title = title,
            TitleArabic = titleArabic,
            Body = body,
            BodyArabic = bodyArabic,
            ImageUrl = NullIfBlank(row.Cells.GetValueOrDefault("ImageUrl", string.Empty)),
            LinkUrl = NullIfBlank(row.Cells.GetValueOrDefault("LinkUrl", string.Empty)),
            Start = start,
            End = end,
            DisplayOrder = int.TryParse(
                row.Cells.GetValueOrDefault("DisplayOrder", string.Empty), out var order) ? order : 0,
        }, ct);
        return GridRowApplyKind.Created;
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}