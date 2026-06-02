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
    DateTimeOffset CreatedAt);

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
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    double? GeofenceCenterLat = null,
    double? GeofenceCenterLon = null,
    double? GeofenceRadiusMeters = null);

public sealed class AdminCreateHallRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? Floor { get; set; }
    public string? EquipmentNotes { get; set; }
    // P5.1 — D-240: optional GPS geofence (all three set together, or all null).
    public double? GeofenceCenterLat { get; set; }
    public double? GeofenceCenterLon { get; set; }
    public double? GeofenceRadiusMeters { get; set; }
}

public sealed class AdminUpdateHallRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? Floor { get; set; }
    public string? EquipmentNotes { get; set; }
    public bool IsActive { get; set; } = true;
    // P5.1 — D-240: optional GPS geofence (all three set together, or all null).
    public double? GeofenceCenterLat { get; set; }
    public double? GeofenceCenterLon { get; set; }
    public double? GeofenceRadiusMeters { get; set; }
}
