// Tests: SIMF.Api.Tests/MediaPartnersExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.PublicRelations;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/media-partners/export</c> — the grid export for
/// media partners. All the work lives in
/// <see cref="AdminGridExportEndpoint{TRow}"/>; this subclass only declares the
/// route, permission, sheet/file names, the column layout (mirroring the
/// MediaPartners grid), and how to list + identify a media-partner row.
/// </summary>
public sealed class ExportMediaPartnersEndpoint(IAdminMediaPartnerService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminMediaPartnerSummary>(exporter)
{
    protected override string RoutePath => "/admin/media-partners/export";
    protected override string Permission => PermissionCatalog.MediaPartners.Export;
    protected override string SheetName => "MediaPartners";
    protected override string FilePrefix => "simf-media-partners";

    protected override IReadOnlyList<GridExcelColumn<AdminMediaPartnerSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminMediaPartnerSummary>> _columns =
    [
        new("Name", row => row.Name),
        new("NameArabic", row => row.NameArabic),
        new("LogoRelativePath", row => row.LogoRelativePath),
        new("Url", row => row.Url),
        new("DisplayOrder", row => row.DisplayOrder),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminMediaPartnerSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Items;

    protected override Guid IdOf(AdminMediaPartnerSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/media-partners/import</c> — the grid import
/// (insert-only) for media partners. The base does the upload defence, parse and
/// per-row error aggregation; this subclass binds one row to
/// <see cref="AdminCreateMediaPartnerRequest"/> and creates it (the service
/// rejects a duplicate English name → a per-row error, not a batch abort).
/// </summary>
public sealed class ImportMediaPartnersEndpoint(IAdminMediaPartnerService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/media-partners/import";
    protected override string Permission => PermissionCatalog.MediaPartners.Import;
    protected override string SheetName => "MediaPartners";
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

        var nameArabic = row.Cells.GetValueOrDefault("NameArabic", string.Empty);
        if (string.IsNullOrWhiteSpace(nameArabic))
        {
            throw new DataValidationException(
                "The Arabic name is required.",
                "الاسم بالعربية مطلوب.");
        }

        await service.CreateAsync(actorId, new AdminCreateMediaPartnerRequest(
            Name: name,
            NameArabic: nameArabic,
            LogoRelativePath: NullIfBlank(row.Cells.GetValueOrDefault("LogoRelativePath", string.Empty)),
            Url: NullIfBlank(row.Cells.GetValueOrDefault("Url", string.Empty)),
            DisplayOrder: int.TryParse(
                row.Cells.GetValueOrDefault("DisplayOrder", string.Empty), out var order) ? order : 0),
            ct);
        return GridRowApplyKind.Created;
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}