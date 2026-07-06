// Tests: SIMF.Api.Tests/VenueMapExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.Exhibition.Abstractions;
using SIMF.Application.Programme.Abstractions;
using SIMF.Application.Venue.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/venue-map/export</c> — the D-356 grid export for the 2D
/// venue-map nodes (SIMF-FDS-006 §5.3, FR-605). All the work lives in
/// <see cref="AdminGridExportEndpoint{TRow}"/>; this subclass only declares the
/// route, permission, sheet/file names, the column layout (mirroring the grid)
/// and how to list + identify a node. The list call goes through the same
/// <see cref="IVenueMapService.ListAsync"/> the list endpoint uses, so the export
/// honours the same filtering.
/// <para>The node's optional <c>HallId</c> / <c>BoothId</c> are FK guids; the
/// export resolves each to its human-readable <b>code</b> (loaded once in
/// <see cref="ListAsync"/>) so the workbook round-trips back through import. An
/// unresolved id falls back to an empty cell (the link was deactivated).</para>
/// </summary>
public sealed class ExportVenueMapEndpoint(
    IVenueMapService service,
    IAdminHallService hallService,
    IAdminBoothService boothService,
    IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminVenueMapNodeSummary>(exporter)
{
    private Dictionary<Guid, string> _hallCodes = new();
    private Dictionary<Guid, string> _boothCodes = new();
    private bool _lookupsLoaded;

    protected override string RoutePath => "/admin/venue-map/export";
    protected override string Permission => PermissionCatalog.VenueMap.Export;
    protected override string SheetName => "VenueMap";
    protected override string FilePrefix => "simf-venue-map";

    // Instance property (not a static field): the Hall/Booth selectors close over
    // the per-request lookup maps populated in ListAsync. The base reads Columns
    // only after ListAsync has run, so the maps are always loaded by then.
    protected override IReadOnlyList<GridExcelColumn<AdminVenueMapNodeSummary>> Columns =>
    [
        new("Label", row => row.Label),
        new("LabelArabic", row => row.LabelArabic),
        new("Kind", row => row.Kind.ToString()),
        new("X", row => row.X),
        new("Y", row => row.Y),
        new("Hall", row => row.HallId is { } id && _hallCodes.TryGetValue(id, out var code) ? code : string.Empty),
        new("Booth", row => row.BoothId is { } id && _boothCodes.TryGetValue(id, out var code) ? code : string.Empty),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminVenueMapNodeSummary>> ListAsync(
        GridQuery query, CancellationToken ct)
    {
        var nodes = (await service.ListAsync(query, ct)).Items;

        // FastEndpoints creates one endpoint instance per request and the export
        // base calls this once per page, so build the Hall/Booth code maps only on
        // the first page. Both list services clamp Top to 200, so page through each
        // lookup — a venue with >200 halls/booths still resolves every node's code.
        if (!_lookupsLoaded)
        {
            var halls = await GridExportPaging.CollectAllAsync(
                async skip => (await hallService.ListAllAsync(
                    GridExportPaging.Page(new GridQuery(), skip, MaxExportRows), ct)).Items,
                MaxExportRows);
            _hallCodes = halls.ToDictionary(h => h.Id, h => h.Code);

            var booths = await GridExportPaging.CollectAllAsync(
                async skip => (await boothService.ListAllAsync(
                    GridExportPaging.Page(new GridQuery(), skip, MaxExportRows), ct)).Items,
                MaxExportRows);
            _boothCodes = booths.ToDictionary(b => b.Id, b => b.Code);

            _lookupsLoaded = true;
        }

        return nodes;
    }

    protected override Guid IdOf(AdminVenueMapNodeSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/venue-map/import</c> — the D-356 grid import
/// (insert-only) for venue-map nodes. The base does the upload defence, parse and
/// per-row error aggregation; this subclass binds one row to
/// <see cref="AdminCreateVenueMapNodeRequest"/> and creates it.
/// <para>FK / enum handling:
/// <list type="bullet">
/// <item><b>Kind</b> is parsed from its enum name (Hall/Zone/Booth/PointOfInterest,
/// as the export writes) or its raw int; an unknown value raises a per-row error.</item>
/// <item><b>Hall</b> / <b>Booth</b> are <i>optional</i> links resolved by their
/// human-readable <b>code</b> (the same the export writes) against the active
/// hall / booth lists. Blank leaves the link unset; a non-blank code that does
/// not match any active record raises a per-row error rather than aborting the
/// batch.</item>
/// </list>
/// The lookup maps are loaded lazily once per request and cached for the rest of
/// the batch (FastEndpoints creates one endpoint instance per request).</para>
/// </summary>
public sealed class ImportVenueMapEndpoint(
    IVenueMapService service,
    IAdminHallService hallService,
    IAdminBoothService boothService,
    IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    // Import batches can reference any hall/booth by code; page the whole list (the
    // list services clamp Top to 200) up to this bound so a >200-hall/booth venue
    // still resolves. Keep equal to AdminGridExportEndpoint.MaxExportRows — the
    // import must resolve every FK a matching export could have written (D-644/645).
    private const int LookupPageCap = 5_000;
    private Dictionary<string, Guid>? _hallsByCode;
    private Dictionary<string, Guid>? _boothsByCode;

    protected override string RoutePath => "/admin/venue-map/import";
    protected override string Permission => PermissionCatalog.VenueMap.Import;
    protected override string SheetName => "VenueMap";
    protected override IReadOnlyList<string> RequiredHeaders => ["Label", "LabelArabic", "Kind"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("Label", out var label) ? label : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var label = row.Cells.GetValueOrDefault("Label", string.Empty);
        var labelArabic = row.Cells.GetValueOrDefault("LabelArabic", string.Empty);
        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(labelArabic))
        {
            throw new DataValidationException(
                "Both the English and Arabic labels are required.",
                "كلا التسميتين بالإنجليزية والعربية مطلوبتان.");
        }

        var kind = ParseKind(row.Cells.GetValueOrDefault("Kind", string.Empty));
        var hallId = await ResolveHallAsync(row.Cells.GetValueOrDefault("Hall", string.Empty), ct);
        var boothId = await ResolveBoothAsync(row.Cells.GetValueOrDefault("Booth", string.Empty), ct);

        await service.CreateAsync(actorId, new AdminCreateVenueMapNodeRequest
        {
            Label = label.Trim(),
            LabelArabic = labelArabic.Trim(),
            Kind = kind,
            X = ParseCoordinate(row.Cells.GetValueOrDefault("X", string.Empty)),
            Y = ParseCoordinate(row.Cells.GetValueOrDefault("Y", string.Empty)),
            HallId = hallId,
            BoothId = boothId,
        }, ct);
        return GridRowApplyKind.Created;
    }

    // Accepts the enum name (Hall/Zone/Booth/PointOfInterest, case-insensitive,
    // as the export writes) or its raw int. Keep aligned with VenueMapNodeKind.
    private static VenueMapNodeKind ParseKind(string value)
    {
        var trimmed = value.Trim();
        if (Enum.TryParse<VenueMapNodeKind>(trimmed, ignoreCase: true, out var byName)
            && Enum.IsDefined(byName))
        {
            return byName;
        }
        throw new DataValidationException(
            "The kind must be one of Hall, Zone, Booth or PointOfInterest.",
            "يجب أن يكون النوع أحد: قاعة أو منطقة أو جناح أو نقطة اهتمام.");
    }

    private static double ParseCoordinate(string value) =>
        double.TryParse(value.Trim(), out var v) ? v : 0;

    private async Task<Guid?> ResolveHallAsync(string code, CancellationToken ct)
    {
        var trimmed = code.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;

        _hallsByCode ??= (await GridExportPaging.CollectAllAsync(
                async skip => (await hallService.ListAllAsync(
                    GridExportPaging.Page(new GridQuery(), skip, LookupPageCap), ct)).Items,
                LookupPageCap))
            .GroupBy(h => h.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        if (_hallsByCode.TryGetValue(trimmed, out var id)) return id;
        throw new DataValidationException(
            $"No active hall has the code \"{trimmed}\".",
            $"لا توجد قاعة نشطة بالرمز \"{trimmed}\".");
    }

    private async Task<Guid?> ResolveBoothAsync(string code, CancellationToken ct)
    {
        var trimmed = code.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;

        _boothsByCode ??= (await GridExportPaging.CollectAllAsync(
                async skip => (await boothService.ListAllAsync(
                    GridExportPaging.Page(new GridQuery(), skip, LookupPageCap), ct)).Items,
                LookupPageCap))
            .GroupBy(b => b.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        if (_boothsByCode.TryGetValue(trimmed, out var id)) return id;
        throw new DataValidationException(
            $"No active booth has the code \"{trimmed}\".",
            $"لا يوجد جناح نشط بالرمز \"{trimmed}\".");
    }
}
