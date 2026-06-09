// Tests: SIMF.Api.Tests/HallsExcelTests.cs
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Api.Endpoints.Admin;

/// <summary>
/// <c>POST /api/v1/admin/halls/export</c> — the D-356 grid export for Halls.
/// All the work lives in <see cref="AdminGridExportEndpoint{TRow}"/>; this
/// subclass only declares the route, permission, sheet/file names, the column
/// layout (mirroring the CP Halls grid), and how to list + identify a hall row.
/// </summary>
public sealed class ExportHallsEndpoint(IAdminHallService service, IGridExcelExporter exporter)
    : AdminGridExportEndpoint<AdminHallSummary>(exporter)
{
    protected override string RoutePath => "/admin/halls/export";
    protected override string Permission => PermissionCatalog.Halls.Export;
    protected override string SheetName => "Halls";
    protected override string FilePrefix => "simf-halls";

    protected override IReadOnlyList<GridExcelColumn<AdminHallSummary>> Columns => _columns;

    private static readonly IReadOnlyList<GridExcelColumn<AdminHallSummary>> _columns =
    [
        new("Code", row => row.Code),
        new("Name", row => row.Name),
        new("NameArabic", row => row.NameArabic),
        new("Capacity", row => row.Capacity),
        new("Floor", row => row.Floor),
        new("IsActive", row => row.IsActive),
    ];

    protected override async Task<IReadOnlyList<AdminHallSummary>> ListAsync(
        GridQuery query, CancellationToken ct) =>
        (await service.ListAllAsync(query, ct)).Items;

    protected override Guid IdOf(AdminHallSummary row) => row.Id;
}

/// <summary>
/// <c>POST /api/v1/admin/halls/import</c> — the D-356 grid import for Halls
/// (insert-only). The base does the upload defence, parse and per-row error
/// aggregation; this subclass binds one row to <see cref="AdminCreateHallRequest"/>
/// and creates it (the service rejects a duplicate Code → a per-row error, not a
/// batch abort). Code is the resource's unique key, so it is the row key echoed
/// back on a per-row error.
/// </summary>
public sealed class ImportHallsEndpoint(IAdminHallService service, IGridExcelImporter importer)
    : AdminGridImportEndpoint(importer)
{
    protected override string RoutePath => "/admin/halls/import";
    protected override string Permission => PermissionCatalog.Halls.Import;
    protected override string SheetName => "Halls";
    protected override IReadOnlyList<string> RequiredHeaders => ["Code", "Name", "NameArabic"];

    protected override string? RowKey(GridImportRow row) =>
        row.Cells.TryGetValue("Code", out var code) ? code : null;

    protected override async Task<GridRowApplyKind> ApplyRowAsync(
        Guid actorId, GridImportRow row, CancellationToken ct)
    {
        var code = row.Cells.GetValueOrDefault("Code", string.Empty);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DataValidationException(
                "The hall code is required.",
                "رمز القاعة مطلوب.");
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

        await service.CreateAsync(actorId, new AdminCreateHallRequest
        {
            Code = code,
            Name = name,
            NameArabic = nameArabic,
            Capacity = int.TryParse(
                row.Cells.GetValueOrDefault("Capacity", string.Empty), out var capacity) ? capacity : 0,
            Floor = row.Cells.GetValueOrDefault("Floor", string.Empty) is { Length: > 0 } floor
                ? floor
                : null,
        }, ct);
        return GridRowApplyKind.Created;
    }
}
