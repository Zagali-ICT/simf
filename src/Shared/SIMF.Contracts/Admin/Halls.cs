namespace SIMF.Contracts.Admin;

/// <summary>One row in the admin Halls grid (D-134 Sprint B —
/// SIMF-FDS-004 §5.2).</summary>
public sealed record AdminHallSummary(
    Guid Id,
    string Code,
    string Name,
    string NameArabic,
    int Capacity,
    string? Floor,
    bool IsActive,
    DateTime CreatedAt,
    // SIMF-FDS-013 — D-248: the hall's purpose (HallPurpose; 0 = General).
    // Appended (defaulted) so the wire contract stays append-only.
    int Purpose = 0,
    // Carried so the grid Excel export can round-trip them (not rendered
    // as grid columns). Optional; the geofence triple is all-three-or-none.
    string? EquipmentNotes = null,
    double? GeofenceCenterLat = null,
    double? GeofenceCenterLon = null,
    double? GeofenceRadiusMeters = null,
    int SeatSelectionMode = 0,
    // The hall's own arrival grace in minutes; null = inherit the global
    // WalkInMode value. Carried for the same D-506 reason as the geofence triple:
    // without it, an Excel round-trip of the halls grid would silently wipe it.
    int? ArrivalGraceMinutes = null);

/// <summary>Full hall detail (Details + Edit modals). P5.1 — D-240: the optional
/// GPS geofence (centre lat/lon + radius in metres) is appended (all null when
/// not configured); the wire contract is append-only (D-219).</summary>
public sealed record AdminHallDetail(
    Guid Id,
    string Code,
    string Name,
    string NameArabic,
    int Capacity,
    string? Floor,
    string? EquipmentNotes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    double? GeofenceCenterLat = null,
    double? GeofenceCenterLon = null,
    double? GeofenceRadiusMeters = null,
    // The hall's seat-selection mode (SeatSelectionMode; 0 = AssignedSeat,
    // 1 = OpenSeating). Appended (defaulted) so the wire stays append-only.
    int SeatSelectionMode = 0,
    // Arrival grace in minutes (0..240); null = inherit the global value.
    int? ArrivalGraceMinutes = null);

public sealed class AdminCreateHallRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? Floor { get; set; }
    public string? EquipmentNotes { get; set; }
    // Optional GPS geofence (all three set together, or all null).
    public double? GeofenceCenterLat { get; set; }
    public double? GeofenceCenterLon { get; set; }
    public double? GeofenceRadiusMeters { get; set; }
    // 0 = AssignedSeat (pick a seat), 1 = OpenSeating (general admission).
    public int SeatSelectionMode { get; set; }
    // How far outside a session's window this hall still admits an
    // arrival, in minutes (0..240). Null = inherit the global WalkInMode value.
    public int? ArrivalGraceMinutes { get; set; }
}

/// <summary>Not sealed: the admin update endpoint binds {id}+body via a derived
/// route class (D-505, mirroring <c>UpdateExhibitorRoute</c>/<c>UpdateSponsorRoute</c>)
/// so it cannot drop a field at bind time — the GPS geofence was being wiped on
/// every edit because the old inline bind model omitted it.</summary>
public class AdminUpdateHallRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? Floor { get; set; }
    public string? EquipmentNotes { get; set; }
    public bool IsActive { get; set; } = true;
    // Optional GPS geofence (all three set together, or all null).
    public double? GeofenceCenterLat { get; set; }
    public double? GeofenceCenterLon { get; set; }
    public double? GeofenceRadiusMeters { get; set; }
    // 0 = AssignedSeat (pick a seat), 1 = OpenSeating (general admission).
    public int SeatSelectionMode { get; set; }
    // How far outside a session's window this hall still admits an
    // arrival, in minutes (0..240). Null = inherit the global WalkInMode value.
    public int? ArrivalGraceMinutes { get; set; }
}
