using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class HallsAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly Model _model = new();
    private string _capacityInput = "0";
    // D-485 — seat-selection mode ("0" = AssignedSeat, "1" = OpenSeating).
    private string _seatModeInput = "0";
    private static readonly string[] _seatModeOptions = { "0", "1" };
    private string _geoLatInput = string.Empty;
    private string _geoLonInput = string.Empty;
    private string _geoRadiusInput = string.Empty;
    private EditContext _editContext = default!;
    private bool _busy;
    private string? _error;

    protected override void OnInitialized()
    {
        if (Initial is not null)
        {
            _model.Code = Initial.Code;
            _model.Name = Initial.Name;
            _model.NameArabic = Initial.NameArabic;
            _model.Floor = Initial.Floor ?? string.Empty;
            _model.EquipmentNotes = Initial.EquipmentNotes ?? string.Empty;
            _model.IsActive = Initial.IsActive;
            _capacityInput = Initial.Capacity.ToString();
            _seatModeInput = Initial.SeatSelectionMode.ToString();
            _geoLatInput = Initial.GeofenceCenterLat?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            _geoLonInput = Initial.GeofenceCenterLon?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            _geoRadiusInput = Initial.GeofenceRadiusMeters?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }
        _editContext = new EditContext(_model);
    }

    private async Task HandleSubmitAsync()
    {
        if (_busy) return;
        _error = null;

        if (string.IsNullOrWhiteSpace(_model.Code) || _model.Code.Length is < 2 or > 16)
        { _error = L["Admin.Halls.Field.CodeInvalid"]; return; }
        if (string.IsNullOrWhiteSpace(_model.Name) || _model.Name.Length > 128)
        { _error = L["Admin.Halls.Field.NameInvalid"]; return; }
        if (string.IsNullOrWhiteSpace(_model.NameArabic) || _model.NameArabic.Length > 128)
        { _error = L["Admin.Halls.Field.NameArabicInvalid"]; return; }
        if (!int.TryParse(_capacityInput, out var capacity) || capacity < 0)
        { _error = L["Admin.Halls.Field.CapacityInvalid"]; return; }
        if (!TryParseGeofence(out var geoLat, out var geoLon, out var geoRadius))
        { _error = L["Admin.Halls.Field.GeofenceInvalid"]; return; }

        _busy = true;
        try
        {
            var result = await SendAsync(
                JS,
                "/account/api/admin/halls",
                $"/account/api/admin/halls/{Initial?.Id}",
                new AdminCreateHallRequest
                {
                    Code = _model.Code.Trim().ToUpperInvariant(),
                    Name = _model.Name.Trim(),
                    NameArabic = _model.NameArabic.Trim(),
                    Capacity = capacity,
                    Floor = string.IsNullOrWhiteSpace(_model.Floor) ? null : _model.Floor.Trim(),
                    EquipmentNotes = string.IsNullOrWhiteSpace(_model.EquipmentNotes) ? null : _model.EquipmentNotes.Trim(),
                    GeofenceCenterLat = geoLat,
                    GeofenceCenterLon = geoLon,
                    GeofenceRadiusMeters = geoRadius,
                    SeatSelectionMode = SeatModeValue(),
                },
                new AdminUpdateHallRequest
                {
                    Code = _model.Code.Trim().ToUpperInvariant(),
                    Name = _model.Name.Trim(),
                    NameArabic = _model.NameArabic.Trim(),
                    Capacity = capacity,
                    Floor = string.IsNullOrWhiteSpace(_model.Floor) ? null : _model.Floor.Trim(),
                    EquipmentNotes = string.IsNullOrWhiteSpace(_model.EquipmentNotes) ? null : _model.EquipmentNotes.Trim(),
                    IsActive = _model.IsActive,
                    GeofenceCenterLat = geoLat,
                    GeofenceCenterLon = geoLon,
                    GeofenceRadiusMeters = geoRadius,
                    SeatSelectionMode = SeatModeValue(),
                });

            if (!result.Succeeded)
            {
                _error = result.ServerMessage ?? L["Admin.Halls.Fallback"];
            }
        }
        finally { _busy = false; }
    }

    // P5.1 — D-240: parse the geofence inputs. All three together (a valid
    // coordinate + positive radius ≤ 100 km), or all three empty (no geofence).
    private bool TryParseGeofence(out double? lat, out double? lon, out double? radius)
    {
        lat = lon = radius = null;
        var latRaw = (_geoLatInput ?? string.Empty).Trim();
        var lonRaw = (_geoLonInput ?? string.Empty).Trim();
        var radiusRaw = (_geoRadiusInput ?? string.Empty).Trim();

        if (latRaw.Length == 0 && lonRaw.Length == 0 && radiusRaw.Length == 0)
        {
            return true; // no geofence configured
        }
        if (latRaw.Length == 0 || lonRaw.Length == 0 || radiusRaw.Length == 0)
        {
            return false; // partial
        }
        if (!double.TryParse(latRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var latV)
            || !double.TryParse(lonRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var lonV)
            || !double.TryParse(radiusRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var radiusV))
        {
            return false;
        }
        if (latV is < -90 or > 90 || lonV is < -180 or > 180 || radiusV is <= 0 or > 100_000)
        {
            return false;
        }
        lat = latV; lon = lonV; radius = radiusV;
        return true;
    }

    // D-485 — the hall's seat-selection mode select ("0" = assigned, "1" = open).
    private string SeatModeLabel(string id) => id switch
    {
        "1" => L["Admin.Halls.SeatMode.Open"],
        _ => L["Admin.Halls.SeatMode.Assigned"],
    };

    private int SeatModeValue() => int.TryParse(_seatModeInput, out var m) ? m : 0;

    private sealed class Model
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NameArabic { get; set; } = string.Empty;
        public string Floor { get; set; } = string.Empty;
        public string EquipmentNotes { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
