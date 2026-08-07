// Tests: SIMF.Api.Tests/SponsorsExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.Sponsors.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/sponsors/export</c> — the grid export for
/// sponsors. All the work lives in <see cref="AdminGridExportEndpoint{TRow}"/>;
/// this subclass only declares the route, permission, sheet/file names, the
/// column layout (mirroring the Sponsors grid — Tier is exported by its display
/// name so the workbook round-trips back through import) and how to list +
/// identify a sponsor row (the same <see cref="IAdminSponsorService.ListAllAsync"/>
/// the list endpoint calls).
/// </summary>
public sealed class ExportSponsorsEndpoint(IAdminSponsorService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminSponsorSummary>(exporter)
{
    protected override string RoutePath => "/admin/sponsors/export";
    protected override string Permission => PermissionCatalog.Sponsors.Export;
    protected override string SheetName => "Sponsors";
    protected override string FilePrefix => "simf-sponsors";

    protected override IReadOnlyList<GridExcelColumn<AdminSponsorSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminSponsorSummary>> _columns =
    [
        new("NameEn", row => row.NameEn),
        new("NameAr", row => row.NameAr),
        new("Tier", row => row.TierName),
        new("LogoRelativePath", row => row.LogoRelativePath),
        new("Url", row => row.Url),
        new("DisplayOrder", row => row.DisplayOrder),
        new("IsActive", row => row.IsActive),
        // Round-trip the bilingual tagline + about (appended so the
        // existing column order is unchanged; import binds by header name).
        new("Tagline", row => row.Tagline),
        new("TaglineArabic", row => row.TaglineArabic),
        new("About", row => row.About),
        new("AboutArabic", row => row.AboutArabic),
    ];

    protected override async Task<IReadOnlyList<AdminSponsorSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Items;

    protected override Guid IdOf(AdminSponsorSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/sponsors/import</c> — the grid import
/// (insert-only) for sponsors. The base does the upload defence, parse and
/// per-row error aggregation; this subclass binds one row to
/// <see cref="AdminCreateSponsorRequest"/> and creates it. Tier is parsed from
/// its display name (Platinum/Gold/Silver/Bronze) — the same set the export
/// writes — back to its int value; an unknown tier raises a per-row error
/// rather than aborting the batch.
/// <para><b>Omitted column:</b> <c>ContactId</c> (the optional shared-Contact
/// link) cannot be expressed as plain text in a bulk
/// import — it is a directory FK chosen with the ContactPicker — so import
/// always leaves it unset; an admin links a contact afterwards via Edit.</para>
/// </summary>
public sealed class ImportSponsorsEndpoint(IAdminSponsorService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/sponsors/import";
    protected override string Permission => PermissionCatalog.Sponsors.Import;
    protected override string SheetName => "Sponsors";
    protected override IReadOnlyList<string> RequiredHeaders => ["NameEn", "NameAr", "Tier"];

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
                "الاسم بالإنجليزية والعربية مطلوبان.");
        }

        var tier = ParseTier(row.Cells.GetValueOrDefault("Tier", string.Empty));

        await service.CreateAsync(actorId, new AdminCreateSponsorRequest
        {
            NameEn = nameEn,
            NameAr = nameAr,
            Tier = tier,
            LogoRelativePath = NullIfBlank(row.Cells.GetValueOrDefault("LogoRelativePath", string.Empty)),
            Url = NullIfBlank(row.Cells.GetValueOrDefault("Url", string.Empty)),
            DisplayOrder = int.TryParse(
                row.Cells.GetValueOrDefault("DisplayOrder", string.Empty), out var order) ? order : 0,
            // Optional bilingual tagline + about (CreateAsync trims and
            // length-guards them; absent columns simply stay null).
            Tagline = NullIfBlank(row.Cells.GetValueOrDefault("Tagline", string.Empty)),
            TaglineArabic = NullIfBlank(row.Cells.GetValueOrDefault("TaglineArabic", string.Empty)),
            About = NullIfBlank(row.Cells.GetValueOrDefault("About", string.Empty)),
            AboutArabic = NullIfBlank(row.Cells.GetValueOrDefault("AboutArabic", string.Empty)),
        }, ct);
        return GridRowApplyKind.Created;
    }

    // Maps a Tier cell back to its int value. Accepts either the display name
    // (Platinum/Gold/Silver/Bronze, as the export writes) or the raw int — keep
    // aligned with SIMF.Domain.Sponsors.SponsorTier.
    private static int ParseTier(string value)
    {
        var trimmed = value.Trim();
        if (int.TryParse(trimmed, out var raw) && raw is 10 or 20 or 30 or 40)
        {
            return raw;
        }
        return trimmed.ToLowerInvariant() switch
        {
            "platinum" => 10,
            "gold" => 20,
            "silver" => 30,
            "bronze" => 40,
            _ => throw new DataValidationException(
                "The tier must be one of Platinum, Gold, Silver or Bronze.",
                "يجب أن تكون الفئة إحدى: بلاتيني أو ذهبي أو فضي أو برونزي."),
        };
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
