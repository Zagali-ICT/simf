using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>
/// One venue hall or room. <see cref="Code"/> is the stable short identifier the
/// venue team works in ("H1", "A201"); the bilingual <see cref="Name"/> and
/// <see cref="NameArabic"/> pair is what visitors see in the agenda.
/// </summary>
public class Hall : BaseAuditEntity
{
    /// <summary>Unique, and uppercased on write, so a lookup must uppercase too.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>The seat cap for every session held here, unless that session sets
    /// its own <c>Session.CapacityOverride</c>. A seat layout may not total more
    /// than this, and the hall cannot be shrunk below what it has already
    /// committed: its layout total, or the largest held reservation count on any
    /// one of its sessions.</summary>
    public int Capacity { get; set; }

    /// <summary>A label rather than a number: "Ground", "Level 2".</summary>
    public string? Floor { get; set; }

    /// <summary>Covers accessibility as well as equipment, despite the name.</summary>
    public string? EquipmentNotes { get; set; }

    /// <summary>What the hall is designated for. General is the unspecialised
    /// state and may back both <c>Session.HallId</c> and <c>Booth.HallId</c>;
    /// dedicating a hall to Booth, Session or Meeting narrows it, and the Control
    /// Panel then offers the matching layout and allocation tools. A business
    /// meeting or a <c>MeetingTable</c> may only target a Meeting or General
    /// hall.</summary>
    public HallPurpose Purpose { get; set; } = HallPurpose.General;

    /// <summary>How attendees join sessions held here: AssignedSeat picks a
    /// specific seat out of the hall's layout, OpenSeating is a bulk join with no
    /// seat at all. A single session may override it via
    /// <c>Session.SeatSelectionModeOverride</c>.</summary>
    public SeatSelectionMode SeatSelectionMode { get; set; } = SeatSelectionMode.AssignedSeat;

    // The optional GPS geofence: a centre in WGS-84 degrees (latitude -90..90,
    // longitude -180..180) and a radius in metres above zero. All three are null
    // together or set together, which the CK_Halls_Geofence check constraint
    // enforces. Null is the ordinary state, and a hall without a geofence records
    // arrivals by QR door scan only; where one is set, an attendee whose reported
    // position falls inside the radius counts as arrived at the hall.
    public double? GeofenceCenterLat { get; set; }
    public double? GeofenceCenterLon { get; set; }
    public double? GeofenceRadiusMeters { get; set; }

    /// <summary>How far outside a session's [Start, End] window this hall still
    /// accepts an arrival, in minutes (0..240). Null inherits and a stored 0 is a
    /// real zero: the order is session override, then hall, then the global
    /// <c>WalkInMode.ArrivalGraceMinutes</c>. Resolve it through
    /// <c>WalkInModeOptions.ResolveArrivalGraceMinutes</c> rather than by hand, so
    /// the door, the admin list and the detail read cannot disagree.
    ///
    /// <para>It exists so a hall that fills slowly, a keynote auditorium where the
    /// queue forms long before the doors nominally open, can widen its own window
    /// without arming the global walk-in capability, which is a server-access-only
    /// switch that also relaxes an approval gate. A single session may override it
    /// again via <c>Session.ArrivalGraceMinutesOverride</c>.</para></summary>
    public int? ArrivalGraceMinutes { get; set; }
}
