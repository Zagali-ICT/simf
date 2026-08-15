using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>One venue hall or room, keyed for the venue team by
/// <see cref="Code"/> ("H1", "A201").</summary>
public class Hall : BaseAuditEntity
{
    /// <summary>Uppercased on write, so a lookup must uppercase too.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Seat cap for every session held here, unless the session sets its
    /// own <c>Session.CapacityOverride</c>. Cannot be shrunk below the hall's
    /// layout total or its largest held reservation count.</summary>
    public int Capacity { get; set; }

    /// <summary>A label rather than a number: "Ground", "Level 2".</summary>
    public string? Floor { get; set; }

    /// <summary>Covers accessibility as well as equipment.</summary>
    public string? EquipmentNotes { get; set; }

    /// <summary>General may back both <c>Session.HallId</c> and
    /// <c>Booth.HallId</c>; a meeting may target only Meeting or General.</summary>
    public HallPurpose Purpose { get; set; } = HallPurpose.General;

    /// <summary>Overridable per session via <c>Session.SeatSelectionModeOverride</c>.</summary>
    public SeatSelectionMode SeatSelectionMode { get; set; } = SeatSelectionMode.AssignedSeat;

    // Optional geofence, all three null together or set together. Without one the
    // hall records arrivals by QR door scan only.
    public double? GeofenceCenterLat { get; set; }
    public double? GeofenceCenterLon { get; set; }
    public double? GeofenceRadiusMeters { get; set; }

    /// <summary>Minutes outside a session's window this hall still accepts an
    /// arrival. Null inherits, a stored 0 is a real zero; resolve the
    /// session-hall-global order through
    /// <c>WalkInModeOptions.ResolveArrivalGraceMinutes</c>.</summary>
    public int? ArrivalGraceMinutes { get; set; }
}
