using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>
/// One venue hall / room (SIMF-FDS-004 §5.2). Halls host Sessions —
/// Sessions reference a hall via <c>HallId</c> in the later Sprint B
/// commit. <see cref="Code"/> is the stable short identifier the venue
/// team uses ("H1", "A201"); bilingual <see cref="Name"/>/<see cref="NameArabic"/>
/// are what visitors see in the agenda.
/// </summary>
public class Hall : BaseAuditEntity
{
    /// <summary>Short stable code (2–16 chars; unique; uppercased).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>English display name (1–128 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic display name (1–128 chars).</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Seating / standing capacity (≥ 0). Drives the booking
    /// cap on Sessions assigned to this hall.</summary>
    public int Capacity { get; set; }

    /// <summary>Optional floor label (e.g. "Ground", "Level 2"). 1–32 chars.</summary>
    public string? Floor { get; set; }

    /// <summary>Optional free-text notes on equipment + accessibility
    /// (≤ 1024 chars).</summary>
    public string? EquipmentNotes { get; set; }

    /// <summary>SIMF-FDS-013 — D-248: what the hall is designated for. Defaults to
    /// <see cref="HallPurpose.General"/> (value 0) — every pre-existing hall keeps
    /// the un-specialised behaviour it had before this column was added (it may
    /// back both <c>Session.HallId</c> and <c>Booth.HallId</c>). An admin may
    /// dedicate a hall to a single purpose (Booth / Session / Meeting) so the
    /// Control Panel surfaces the matching layout + allocation tools. A
    /// <c>MeetingTable</c> / business-meeting may only target a Meeting or General
    /// hall.</summary>
    public HallPurpose Purpose { get; set; } = HallPurpose.General;

    /// <summary>D-485 (owner batch 2026-06-21) — how attendees join sessions held
    /// in this hall: <see cref="SeatSelectionMode.AssignedSeat"/> (pick a specific
    /// seat from the layout — the pre-existing behaviour, value 0 so every existing
    /// hall is unchanged) or <see cref="SeatSelectionMode.OpenSeating"/> (bulk join,
    /// no seat). A <c>Session</c> may override this per-session via
    /// <c>Session.SeatSelectionModeOverride</c>.</summary>
    public SeatSelectionMode SeatSelectionMode { get; set; } = SeatSelectionMode.AssignedSeat;

    /// <summary>P5.1 — D-240 (FDS-003 §5.4 + OI-2): the hall's optional GPS
    /// geofence centre latitude (WGS-84 degrees, −90..90). All three geofence
    /// columns are null together when no geofence is configured — the hall then
    /// records arrivals by QR door scan only. Set together by the admin in the
    /// CP; the event team seeds the real coordinates (G-OI-2, "ship empty").</summary>
    public double? GeofenceCenterLat { get; set; }

    /// <summary>P5.1 — D-240: the geofence centre longitude (−180..180), paired
    /// with <see cref="GeofenceCenterLat"/>.</summary>
    public double? GeofenceCenterLon { get; set; }

    /// <summary>P5.1 — D-240: the geofence radius in metres (&gt; 0). An attendee
    /// whose reported GPS point is within this radius of the centre is treated as
    /// arrived at the hall (<c>Method = Geofence</c>).</summary>
    public double? GeofenceRadiusMeters { get; set; }

    /// <summary>D-839 — how far outside a session's [Start, End] window this hall
    /// still accepts an ARRIVAL, in minutes (0..240). Null means "inherit the
    /// global <c>WalkInMode.ArrivalGraceMinutes</c>", which is the state every
    /// pre-existing hall is in, so behaviour is unchanged until a hall is given
    /// its own value.
    ///
    /// <para>A hall that fills slowly — a keynote auditorium where the queue forms
    /// long before the doors nominally open — can widen its own window without
    /// arming the global walk-in capability, which is a server-access-only switch
    /// that also relaxes an approval gate. Sits beside the geofence triple as the
    /// hall's other arrival knob; a single session may override it via
    /// <c>Session.ArrivalGraceMinutesOverride</c>.</para></summary>
    public int? ArrivalGraceMinutes { get; set; }
}
