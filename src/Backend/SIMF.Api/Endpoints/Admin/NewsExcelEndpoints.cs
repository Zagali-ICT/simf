// Tests: SIMF.Api.Tests/NewsExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.PublicRelations;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/news/export</c> — the grid export for News
/// articles (PR / marketing). All the work lives in
/// <see cref="AdminGridExportEndpoint{TRow}"/>; this subclass only declares the
/// route, permission, sheet/file names, the column layout (mirroring the News
/// grid), and how to list + identify a news row.
/// </summary>
public sealed class ExportNewsEndpoint(IAdminNewsService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminNewsSummary>(exporter)
{
    protected override string RoutePath => "/admin/news/export";
    protected override string Permission => PermissionCatalog.News.Export;
    protected override string SheetName => "News";
    protected override string FilePrefix => "simf-news";

    protected override IReadOnlyList<GridExcelColumn<AdminNewsSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminNewsSummary>> _columns =
    [
        new("Title", row => row.Title),
        new("TitleArabic", row => row.TitleArabic),
        new("Category", row => row.Category),
        new("CategoryArabic", row => row.CategoryArabic),
        new("PublishedAt", row => row.PublishedAt),
        new("DisplayOrder", row => row.DisplayOrder),
        new("IsActive", row => row.IsActive),
        // Round-trip the Arabic body + excerpt (appended so the existing
        // column order is unchanged; import binds by header name and already reads
        // these cells in ApplyRowAsync).
        new("BodyArabic", row => row.BodyArabic),
        new("ExcerptArabic", row => row.ExcerptArabic),
    ];

    protected override async Task<IReadOnlyList<AdminNewsSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Items;

    protected override Guid IdOf(AdminNewsSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/news/import</c> — the grid import (insert-only)
/// for News articles. The base does the upload defence, parse and per-row error
/// aggregation; this subclass binds one row to <see cref="CreateNewsRequest"/>
/// and creates it (the service rejects a duplicate English title → a per-row
/// error, not a batch abort). The body columns (omitted from the export, which
/// mirrors the lighter grid summary) are required on import because the create
/// contract demands them.
/// </summary>
public sealed class ImportNewsEndpoint(IAdminNewsService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/news/import";
    protected override string Permission => PermissionCatalog.News.Import;
    protected override string SheetName => "News";
    protected override IReadOnlyList<string> RequiredHeaders =>
        ["Title", "TitleArabic", "Body", "BodyArabic", "Category", "CategoryArabic"];

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

        var category = row.Cells.GetValueOrDefault("Category", string.Empty);
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new DataValidationException(
                "The English category is required.",
                "التصنيف بالإنجليزية مطلوب.");
        }

        var categoryArabic = row.Cells.GetValueOrDefault("CategoryArabic", string.Empty);
        if (string.IsNullOrWhiteSpace(categoryArabic))
        {
            throw new DataValidationException(
                "The Arabic category is required.",
                "التصنيف بالعربية مطلوب.");
        }

        var publishedAt = DateTime.TryParse(
            row.Cells.GetValueOrDefault("PublishedAt", string.Empty), out var parsed)
            ? parsed
            : SimfClock.Now;

        await service.CreateAsync(actorId, new CreateNewsRequest
        {
            Title = title,
            TitleArabic = titleArabic,
            Excerpt = NullIfBlank(row.Cells.GetValueOrDefault("Excerpt", string.Empty)),
            ExcerptArabic = NullIfBlank(row.Cells.GetValueOrDefault("ExcerptArabic", string.Empty)),
            Body = body,
            BodyArabic = bodyArabic,
            Category = category,
            CategoryArabic = categoryArabic,
            PublishedAt = publishedAt,
            DisplayOrder = int.TryParse(
                row.Cells.GetValueOrDefault("DisplayOrder", string.Empty), out var order) ? order : 0,
        }, ct);
        return GridRowApplyKind.Created;
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
