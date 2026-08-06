// Tests: SIMF.Api.Tests/BoothsExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.Exhibition.Abstractions;
using SIMF.Application.Exhibitors.Abstractions;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Exhibition;
using SIMF.Contracts.Exhibitors;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/booths/export</c> — the grid export for the
/// Exhibition booths. All the work lives in
/// <see cref="AdminGridExportEndpoint{TRow}"/>; this subclass declares the route,
/// permission, sheet/file names, the column layout (mirroring the Booths grid)
/// and how to list + identify a booth row (the same
/// <see cref="IAdminBoothService.ListAllAsync"/> the list endpoint calls).
/// <para>The two foreign keys are exported by a human-readable natural key so the
/// workbook round-trips back through import: the Exhibitor as its English name
/// and the Hall as its code. The maps are built once per request inside
/// <see cref="ListAsync"/> (the base reads <see cref="Columns"/> straight after),
/// so the column selectors resolve a name without an extra round-trip per row.
/// </para>
/// </summary>
public sealed class ExportBoothsEndpoint(
    IAdminBoothService service,
    IAdminExhibitorService exhibitorService,
    IAdminHallService hallService,
    IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminBoothSummary>(exporter)
{
    protected override string RoutePath => "/admin/booths/export";
    protected override string Permission => PermissionCatalog.Booths.Export;
    protected override string SheetName => "Booths";
    protected override string FilePrefix => "simf-booths";

    // Built per request in ListAsync; the base reads Columns right afterwards.
    private Dictionary<Guid, string> _exhibitorNames = new();
    private Dictionary<Guid, string> _hallCodes = new();

    protected override IReadOnlyList<GridExcelColumn<AdminBoothSummary>> Columns =>
    [
        new("Code", row => row.Code),
        new("Name", row => row.Name),
        new("NameArabic", row => row.NameArabic),
        new("Exhibitor", row => row.ExhibitorId is { } id
            && _exhibitorNames.TryGetValue(id, out var name) ? name : string.Empty),
        new("Sector", row => row.Sector),
        new("Hall", row => row.HallId is { } id
            && _hallCodes.TryGetValue(id, out var code) ? code : string.Empty),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminBoothSummary>> ListAsync(
        GridQuery query, CancellationToken ct)
    {
        var rows = (await service.ListAllAsync(query, ct)).Items;

        // Resolve the two FK display values once for the whole sheet. The
        // lookups are bounded (the services cap Top at 200), so a single read
        // each is fine — the same Top=500 the CP list page uses for these
        // pickers.
        var lookupQuery = new GridQuery { Skip = 0, Top = 500 };
        var exhibitors = (await exhibitorService.ListAllAsync(lookupQuery, ct)).Items;
        var halls = (await hallService.ListAllAsync(lookupQuery, ct)).Items;
        _exhibitorNames = exhibitors.ToDictionary(e => e.Id, e => e.NameEn);
        _hallCodes = halls.ToDictionary(h => h.Id, h => h.Code);

        return rows;
    }

    protected override Guid IdOf(AdminBoothSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/booths/import</c> — the grid import (insert-only)
/// for the Exhibition booths. The base does the upload defence, parse and per-row
/// error aggregation; this subclass binds one row to
/// <see cref="AdminCreateBoothRequest"/> and creates it (the service rejects a
/// duplicate code → a per-row error, not a batch abort).
/// <para>
/// The two foreign keys are resolved from their natural key, the same value the
/// export writes: the Exhibitor from its English name and the Hall from its code
/// (case-insensitive, active only). Both are optional — a blank cell leaves the
/// link unset; a non-blank value that does not resolve to an active row raises a
/// per-row <see cref="DataValidationException"/>.
/// </para>
/// <para><b>Omitted columns:</b> the officer fields, the optional shared-Contact
/// link (a directory FK chosen with the ContactPicker) and
/// the 2D map position cannot be expressed safely as plain text in a bulk import,
/// so import always leaves them unset; an admin sets them afterwards via Edit.
/// </para>
/// </summary>
public sealed class ImportBoothsEndpoint(
    IAdminBoothService service,
    IAdminExhibitorService exhibitorService,
    IAdminHallService hallService,
    IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/booths/import";
    protected override string Permission => PermissionCatalog.Booths.Import;
    protected override string SheetName => "Booths";
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

        var exhibitorId = await ResolveExhibitorAsync(
            row.Cells.GetValueOrDefault("Exhibitor", string.Empty), ct);
        var hallId = await ResolveHallAsync(
            row.Cells.GetValueOrDefault("Hall", string.Empty), ct);

        await service.CreateAsync(actorId, new AdminCreateBoothRequest
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            NameArabic = nameArabic.Trim(),
            ExhibitorId = exhibitorId,
            Sector = NullIfBlank(row.Cells.GetValueOrDefault("Sector", string.Empty)),
            HallId = hallId,
        }, ct);
        return GridRowApplyKind.Created;
    }

    // Resolves an optional Exhibitor by its English name (the value the export
    // writes). Blank → unset. A non-blank value that does not match an active
    // exhibitor is a per-row error rather than a silent skip.
    private async Task<Guid?> ResolveExhibitorAsync(string value, CancellationToken ct)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0) { return null; }

        var exhibitors = (await exhibitorService
            .ListAllAsync(new GridQuery { Top = 500, Search = trimmed }, ct)).Items;
        var match = exhibitors.FirstOrDefault(e => e.IsActive
            && string.Equals(e.NameEn, trimmed, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new DataValidationException(
                $"No active exhibitor named '{trimmed}' was found.",
                $"لم يتم العثور على عارض مفعّل باسم '{trimmed}'.");
        }
        return match.Id;
    }

    // Resolves an optional Hall by its code (the value the export writes). Blank
    // → unset. A non-blank value that does not match an active hall is a per-row
    // error.
    private async Task<Guid?> ResolveHallAsync(string value, CancellationToken ct)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0) { return null; }

        var halls = (await hallService
            .ListAllAsync(new GridQuery { Top = 500, Search = trimmed }, ct)).Items;
        var match = halls.FirstOrDefault(h => h.IsActive
            && string.Equals(h.Code, trimmed, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new DataValidationException(
                $"No active hall with code '{trimmed}' was found.",
                $"لم يتم العثور على قاعة مفعّلة بالرمز '{trimmed}'.");
        }
        return match.Id;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
