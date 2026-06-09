// Tests: SIMF.Api.Tests/ContentBlocksExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Cms.Abstractions;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/content-blocks/export</c> — the D-356 grid export for
/// dynamic CMS content blocks. All the work lives in
/// <see cref="AdminGridExportEndpoint{TRow}"/>; this subclass only declares the
/// route, permission, sheet/file names, the column layout (mirroring the
/// ContentBlocks grid), and how to list + identify a content-block row.
/// </summary>
public sealed class ExportContentBlocksEndpoint(IAdminCmsService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminContentBlockSummary>(exporter)
{
    protected override string RoutePath => "/admin/content-blocks/export";
    protected override string Permission => PermissionCatalog.ContentBlocks.Export;
    protected override string SheetName => "ContentBlocks";
    protected override string FilePrefix => "simf-content-blocks";

    protected override IReadOnlyList<GridExcelColumn<AdminContentBlockSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminContentBlockSummary>> _columns =
    [
        new("Key", row => row.Key),
        new("Content", row => row.Content),
        new("ContentArabic", row => row.ContentArabic),
        new("IsActive", row => row.IsActive),
        new("LastUpdatedAt", row => row.LastUpdatedAt),
    ];

    protected override async Task<IReadOnlyList<AdminContentBlockSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListContentBlocksAsync(query, ct)).Items;

    protected override Guid IdOf(AdminContentBlockSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/content-blocks/import</c> — the D-356 grid import for
/// dynamic CMS content blocks. This resource has no create; it upserts by
/// <c>Key</c>, so a row binds to <see cref="UpsertContentBlockRequest"/> and is
/// created when the key is new, updated in place when it already exists. The
/// base does the upload defence, parse and per-row error aggregation; this
/// subclass decides the per-row outcome by probing the key before the upsert
/// (the service rejects an invalid key/content with an <c>ApiException</c>,
/// which the base records as a per-row error rather than aborting the batch).
/// </summary>
public sealed class ImportContentBlocksEndpoint(IAdminCmsService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/content-blocks/import";
    protected override string Permission => PermissionCatalog.ContentBlocks.Import;
    protected override string SheetName => "ContentBlocks";
    protected override IReadOnlyList<string> RequiredHeaders => ["Key", "Content", "ContentArabic"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("Key", out var key) ? key : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var key = row.Cells.GetValueOrDefault("Key", string.Empty);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DataValidationException(
                "The key is required.",
                "المفتاح مطلوب.");
        }

        var content = row.Cells.GetValueOrDefault("Content", string.Empty);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new DataValidationException(
                "The English content is required.",
                "المحتوى بالإنجليزية مطلوب.");
        }

        var contentArabic = row.Cells.GetValueOrDefault("ContentArabic", string.Empty);
        if (string.IsNullOrWhiteSpace(contentArabic))
        {
            throw new DataValidationException(
                "The Arabic content is required.",
                "المحتوى بالعربية مطلوب.");
        }

        // Upsert-by-key: probe first so the per-row outcome reports Created vs
        // Updated (the service normalises the key, so the raw cell matches).
        var existing = await service.GetContentBlockAsync(key, ct);

        await service.UpsertContentBlockAsync(actorId, new UpsertContentBlockRequest
        {
            Key = key,
            Content = content,
            ContentArabic = contentArabic,
            IsActive = ParseIsActive(row.Cells.GetValueOrDefault("IsActive", string.Empty)),
        }, ct);

        return existing is null ? GridRowApplyKind.Created : GridRowApplyKind.Updated;
    }

    // The IsActive column is optional on import; a blank or unparseable cell
    // defaults to active (matching the upsert request default).
    private static bool ParseIsActive(string value) =>
        !bool.TryParse(value, out var isActive) || isActive;
}
