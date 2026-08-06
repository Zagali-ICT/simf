// Tests: SIMF.Api.Tests/HallsExcelTests.cs
using System.Globalization;
using SIMF.Api.Endpoints.Admin.Grid;
using SIMF.Application.Excel;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Options;
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
        // Round-trip the fields the grid summary now carries (appended so
        // the existing column order is unchanged; import binds by header name).
        // SeatSelectionMode is exported by its display name (AssignedSeat/
        // OpenSeating); the geofence triple is all-three-or-none.
        new("EquipmentNotes", row => row.EquipmentNotes),
        new("GeofenceCenterLat", row => row.GeofenceCenterLat),
        new("GeofenceCenterLon", row => row.GeofenceCenterLon),
        new("GeofenceRadiusMeters", row => row.GeofenceRadiusMeters),
        new("SeatSelectionMode", row => ((SeatSelectionMode)row.SeatSelectionMode).ToString()),
        // Blank means "inherit the system default", so an empty cell is a
        // real value here, not a missing one.
        new("ArrivalGraceMinutes", row => row.ArrivalGraceMinutes),
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
            // Optional fields the grid summary now round-trips. The
            // geofence triple is all-three-or-none; a partial geofence row is
            // rejected by CreateAsync as a per-row error (not a batch abort).
            EquipmentNotes = NullIfBlank(row.Cells.GetValueOrDefault("EquipmentNotes", string.Empty)),
            GeofenceCenterLat = ParseGeo(row.Cells.GetValueOrDefault("GeofenceCenterLat", string.Empty)),
            GeofenceCenterLon = ParseGeo(row.Cells.GetValueOrDefault("GeofenceCenterLon", string.Empty)),
            GeofenceRadiusMeters = ParseGeo(row.Cells.GetValueOrDefault("GeofenceRadiusMeters", string.Empty)),
            SeatSelectionMode = ParseSeatSelectionMode(
                row.Cells.GetValueOrDefault("SeatSelectionMode", string.Empty)),
            ArrivalGraceMinutes = ParseArrivalGrace(
                row.Cells.GetValueOrDefault("ArrivalGraceMinutes", string.Empty)),
        }, ct);
        return GridRowApplyKind.Created;
    }

    // Parses an optional geofence coordinate/radius. Blank → null (the geofence is
    // all-three-or-none and the service validates the partial case). A non-blank,
    // non-numeric value is a per-row error rather than a silent zero.
    private static double? ParseGeo(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }
        throw new DataValidationException(
            "The geofence latitude, longitude and radius must be numbers.",
            "يجب أن تكون قيم خط العرض وخط الطول ونصف القطر للسياج أرقاماً.");
    }

    // Parses the optional arrival grace. Blank stays null, which is the
    // real "inherit the system default" value. A non-blank, out-of-range or
    // non-numeric cell is a per-row error rather than a silent 0, which would
    // otherwise slam the hall's doors shut the moment a session ends.
    private static int? ParseArrivalGrace(string value)
    {
        if (WalkInModeOptions.TryParseArrivalGrace(value, out var minutes))
        {
            return minutes;
        }
        throw new DataValidationException(
            $"Arrival grace must be a whole number of minutes between 0 and {WalkInModeOptions.MaxArrivalGraceMinutes}, or blank.",
            $"يجب أن تكون مهلة الوصول عدداً صحيحاً من الدقائق بين 0 و{WalkInModeOptions.MaxArrivalGraceMinutes}، أو فارغة.");
    }

    // Maps a SeatSelectionMode cell to its int value. Accepts the display name
    // (AssignedSeat/OpenSeating, as the export writes) or the raw int; blank →
    // 0 (AssignedSeat, the default). Unknown non-blank → a per-row error — keep
    // aligned with SIMF.Common.Enums.SeatSelectionMode.
    private static int ParseSeatSelectionMode(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return (int)SeatSelectionMode.AssignedSeat;
        }
        if (int.TryParse(trimmed, out var raw) && Enum.IsDefined(typeof(SeatSelectionMode), raw))
        {
            return raw;
        }
        return trimmed.ToLowerInvariant() switch
        {
            "assignedseat" => (int)SeatSelectionMode.AssignedSeat,
            "openseating" => (int)SeatSelectionMode.OpenSeating,
            _ => throw new DataValidationException(
                "The seat-selection mode must be one of AssignedSeat or OpenSeating.",
                "يجب أن يكون وضع اختيار المقاعد إما AssignedSeat أو OpenSeating."),
        };
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
