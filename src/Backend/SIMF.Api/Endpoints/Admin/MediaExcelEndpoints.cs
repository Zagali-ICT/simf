// Tests: SIMF.Api.Tests/MediaExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.Media.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Media;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/media/export</c> — the D-356 grid export for the Media
/// gallery (Mockup page 30). All the work lives in
/// <see cref="AdminGridExportEndpoint{TRow}"/>; this subclass only declares the
/// route, permission, sheet/file names, the column layout (mirroring the Media
/// grid), and how to list + identify a media row.
/// <para>The image bitmap is held out-of-row (D-90) and is never exported — the
/// workbook carries only the metadata + the external playback <c>Url</c>.</para>
/// </summary>
public sealed class ExportMediaEndpoint(IAdminMediaService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminMediaSummary>(exporter)
{
    protected override string RoutePath => "/admin/media/export";
    protected override string Permission => PermissionCatalog.Media.Export;
    protected override string SheetName => "Media";
    protected override string FilePrefix => "simf-media";

    protected override IReadOnlyList<GridExcelColumn<AdminMediaSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminMediaSummary>> _columns =
    [
        new("Kind", row => row.Kind),
        new("Title", row => row.Title),
        new("TitleArabic", row => row.TitleArabic),
        new("Album", row => row.Album),
        new("AlbumArabic", row => row.AlbumArabic),
        new("HasImage", row => row.HasImage),
        new("Url", row => row.Url),
        new("DisplayOrder", row => row.DisplayOrder),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminMediaSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Items;

    protected override Guid IdOf(AdminMediaSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/media/import</c> — the D-356 grid import (insert-only)
/// for the Media gallery. The base does the upload defence, parse and per-row
/// error aggregation; this subclass binds one row to
/// <see cref="AdminCreateMediaRequest"/> and creates it.
/// <para>Import creates the metadata row only — the image bitmap is uploaded
/// out-of-row afterwards via <c>POST /admin/media/{id}/image</c> (D-90), so no
/// bytes ride the workbook. A Video row requires a <c>Url</c> (its playback
/// source); an Image does not.</para>
/// </summary>
public sealed class ImportMediaEndpoint(IAdminMediaService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/media/import";
    protected override string Permission => PermissionCatalog.Media.Import;
    protected override string SheetName => "Media";
    protected override IReadOnlyList<string> RequiredHeaders => ["Kind"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("Title", out var title) && !string.IsNullOrWhiteSpace(title)
            ? title
            : row.Cells.GetValueOrDefault("Kind");

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var kindText = row.Cells.GetValueOrDefault("Kind", string.Empty);
        if (string.IsNullOrWhiteSpace(kindText))
        {
            throw new DataValidationException(
                "The media kind is required (Image or Video).",
                "نوع الوسائط مطلوب (صورة أو فيديو).");
        }
        if (!Enum.TryParse<MediaKind>(kindText, ignoreCase: true, out var kind)
            || !Enum.IsDefined(kind))
        {
            throw new DataValidationException(
                "The media kind must be Image or Video.",
                "يجب أن يكون نوع الوسائط صورة أو فيديو.");
        }

        var url = NullIfBlank(row.Cells.GetValueOrDefault("Url", string.Empty));
        if (kind == MediaKind.Video && url is null)
        {
            throw new DataValidationException(
                "A video requires a URL.",
                "الفيديو يتطلب رابطًا.");
        }

        await service.CreateAsync(actorId, new AdminCreateMediaRequest
        {
            Kind = kind,
            Title = NullIfBlank(row.Cells.GetValueOrDefault("Title", string.Empty)),
            TitleArabic = NullIfBlank(row.Cells.GetValueOrDefault("TitleArabic", string.Empty)),
            Album = NullIfBlank(row.Cells.GetValueOrDefault("Album", string.Empty)),
            AlbumArabic = NullIfBlank(row.Cells.GetValueOrDefault("AlbumArabic", string.Empty)),
            Url = url,
            DisplayOrder = int.TryParse(
                row.Cells.GetValueOrDefault("DisplayOrder", string.Empty), out var order) ? order : 0,
        }, ct);
        return GridRowApplyKind.Created;
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}