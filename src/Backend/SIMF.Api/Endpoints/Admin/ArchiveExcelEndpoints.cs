// Tests: SIMF.Api.Tests/ArchiveExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Archive.Abstractions;
using SIMF.Application.Excel;
using SIMF.Common;
using SIMF.Contracts.Archive;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/archive/export</c> — the D-356 grid export for archive
/// editions. All the work lives in <see cref="AdminGridExportEndpoint{TRow}"/>;
/// this subclass only declares the route, permission, sheet/file names, the
/// column layout (mirroring the Archive grid), and how to list + identify an
/// edition row.
/// </summary>
public sealed class ExportArchiveEndpoint(IAdminArchiveService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminArchiveEditionSummary>(exporter)
{
    protected override string RoutePath => "/admin/archive/export";
    protected override string Permission => PermissionCatalog.Archive.Export;
    protected override string SheetName => "Archive";
    protected override string FilePrefix => "simf-archive";

    protected override IReadOnlyList<GridExcelColumn<AdminArchiveEditionSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminArchiveEditionSummary>> _columns =
    [
        new("Year", row => row.Year),
        new("TitleEn", row => row.TitleEn),
        new("TitleAr", row => row.TitleAr),
        new("Attendees", row => row.Attendees),
        new("Sessions", row => row.Sessions),
        new("Speakers", row => row.Speakers),
        new("IsActive", row => row.IsActive),
        // D-506 — round-trip the dropped edition fields (summary, location, date
        // label, cover path; appended so the existing column order is unchanged;
        // import binds by header name).
        new("SummaryEn", row => row.SummaryEn),
        new("SummaryAr", row => row.SummaryAr),
        new("LocationEn", row => row.LocationEn),
        new("LocationAr", row => row.LocationAr),
        new("CoverImageRelativePath", row => row.CoverImageRelativePath),
        new("DateLabelEn", row => row.DateLabelEn),
        new("DateLabelAr", row => row.DateLabelAr),
    ];

    protected override async Task<IReadOnlyList<AdminArchiveEditionSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Items;

    protected override Guid IdOf(AdminArchiveEditionSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/archive/import</c> — the D-356 grid import (insert-only)
/// for archive editions. The base does the upload defence, parse and per-row
/// error aggregation; this subclass binds one row to
/// <see cref="CreateArchiveEditionRequest"/> and creates it (the service rejects
/// a duplicate year with a 409 <c>ApiException</c>, which the base records as a
/// per-row error rather than aborting the batch).
/// </summary>
public sealed class ImportArchiveEndpoint(IAdminArchiveService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/archive/import";
    protected override string Permission => PermissionCatalog.Archive.Import;
    protected override string SheetName => "Archive";
    protected override IReadOnlyList<string> RequiredHeaders => ["Year", "TitleEn", "TitleAr"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("Year", out var year) ? year : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var yearText = row.Cells.GetValueOrDefault("Year", string.Empty);
        if (!int.TryParse(yearText, out var year))
        {
            throw new DataValidationException(
                "The year is required and must be a whole number.",
                "العام مطلوب ويجب أن يكون رقماً صحيحاً.");
        }

        var titleEn = row.Cells.GetValueOrDefault("TitleEn", string.Empty);
        if (string.IsNullOrWhiteSpace(titleEn))
        {
            throw new DataValidationException(
                "The English title is required.",
                "العنوان بالإنجليزية مطلوب.");
        }

        var titleAr = row.Cells.GetValueOrDefault("TitleAr", string.Empty);
        if (string.IsNullOrWhiteSpace(titleAr))
        {
            throw new DataValidationException(
                "The Arabic title is required.",
                "العنوان بالعربية مطلوب.");
        }

        await service.CreateAsync(actorId, new CreateArchiveEditionRequest
        {
            Year = year,
            TitleEn = titleEn,
            TitleAr = titleAr,
            SummaryEn = NullIfBlank(row.Cells.GetValueOrDefault("SummaryEn", string.Empty)),
            SummaryAr = NullIfBlank(row.Cells.GetValueOrDefault("SummaryAr", string.Empty)),
            Attendees = int.TryParse(
                row.Cells.GetValueOrDefault("Attendees", string.Empty), out var attendees) ? attendees : 0,
            Sessions = int.TryParse(
                row.Cells.GetValueOrDefault("Sessions", string.Empty), out var sessions) ? sessions : 0,
            Speakers = int.TryParse(
                row.Cells.GetValueOrDefault("Speakers", string.Empty), out var speakers) ? speakers : 0,
            // D-506 — round-trip the remaining dropped edition fields (location,
            // date label, cover path; CreateAsync trims + length-guards them,
            // absent columns simply stay null).
            LocationEn = NullIfBlank(row.Cells.GetValueOrDefault("LocationEn", string.Empty)),
            LocationAr = NullIfBlank(row.Cells.GetValueOrDefault("LocationAr", string.Empty)),
            CoverImageRelativePath = NullIfBlank(
                row.Cells.GetValueOrDefault("CoverImageRelativePath", string.Empty)),
            DateLabelEn = NullIfBlank(row.Cells.GetValueOrDefault("DateLabelEn", string.Empty)),
            DateLabelAr = NullIfBlank(row.Cells.GetValueOrDefault("DateLabelAr", string.Empty)),
        }, ct);
        return GridRowApplyKind.Created;
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
