// Tests: SIMF.Api.Tests/SpeakersExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/speakers/export</c> — the D-356 grid export for the
/// programme speakers (SIMF-DAT-001 §5.4). All the work lives in
/// <see cref="AdminGridExportEndpoint{TRow}"/>; this subclass only declares the
/// route, permission, sheet/file names, the column layout (mirroring the grid's
/// visible columns), and how to list + identify a speaker row. The list call
/// goes through the same <see cref="IAdminSpeakerService.ListAllAsync"/> the
/// list endpoint uses, so the export honours the same filtering.
/// </summary>
public sealed class ExportSpeakersEndpoint(IAdminSpeakerService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminSpeakerSummary>(exporter)
{
    protected override string RoutePath => "/admin/speakers/export";
    protected override string Permission => PermissionCatalog.Speakers.Export;
    protected override string SheetName => "Speakers";
    protected override string FilePrefix => "simf-speakers";

    protected override IReadOnlyList<GridExcelColumn<AdminSpeakerSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminSpeakerSummary>> _columns =
    [
        new("Code", row => row.Code),
        new("Name", row => row.Name),
        new("NameArabic", row => row.NameArabic),
        new("Rank", row => row.Rank),
        new("Country", row => row.CountryNameEn),
        new("DisplayOrder", row => row.DisplayOrder),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminSpeakerSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Items;

    protected override Guid IdOf(AdminSpeakerSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/speakers/import</c> — the D-356 grid import (insert-only).
/// The base does the upload defence, parse and per-row error aggregation; this
/// subclass binds one row to <see cref="AdminCreateSpeakerRequest"/> and creates
/// it (the service rejects a duplicate code → a per-row error, not a batch abort).
/// <para>
/// Bound columns: Code, Name, NameArabic, Rank, DisplayOrder. The Country column
/// (a numeric FK to <c>Country.Id</c>) and the bilingual rich-text, social-URL
/// and consent fields are intentionally <b>omitted</b> from import — they cannot
/// be expressed safely as plain text in a flat sheet, so the admin sets them via
/// the Edit form after the bulk insert. The exported "Country" column carries the
/// display name (read-only) so the workbook stays a faithful snapshot of the grid.
/// </para>
/// </summary>
public sealed class ImportSpeakersEndpoint(IAdminSpeakerService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/speakers/import";
    protected override string Permission => PermissionCatalog.Speakers.Import;
    protected override string SheetName => "Speakers";
    protected override IReadOnlyList<string> RequiredHeaders => ["Code", "Name", "NameArabic"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("Code", out var code) ? code : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var code = row.Cells.GetValueOrDefault("Code", string.Empty);
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length is < 2 or > 16)
        {
            throw new DataValidationException(
                "Code must be between 2 and 16 characters.",
                "يجب أن يكون الرمز بين حرفين و16 حرفًا.");
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

        await service.CreateAsync(actorId, new AdminCreateSpeakerRequest
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            NameArabic = nameArabic.Trim(),
            Rank = NullIfBlank(row.Cells.GetValueOrDefault("Rank", string.Empty)),
            DisplayOrder = int.TryParse(
                row.Cells.GetValueOrDefault("DisplayOrder", string.Empty), out var order) ? order : 0,
        }, ct);
        return GridRowApplyKind.Created;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
