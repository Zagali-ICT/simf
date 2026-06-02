using SIMF.Common.Enums;

namespace SIMF.Domain.Programme;

/// <summary>
/// One venue hall / room (SIMF-FDS-004 §5.2). Halls host Sessions —
/// Sessions reference a hall via <c>HallId</c> in the later Sprint B
/// commit. <see cref="Code"/> is the stable short identifier the venue
/// team uses ("H1", "A201"); bilingual <see cref="Name"/>/<see cref="NameArabic"/>
/// are what visitors see in the agenda.
/// </summary>
public class Hall
{
    public Guid Id { get; set; }

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

    /// <summary>Soft-delete flag.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>SIMF-FDS-013 — D-248: what the hall is designated for. Defaults to
    /// <see cref="HallPurpose.General"/> (value 0) — every pre-existing hall keeps
    /// the un-specialised behaviour it had before this column was added (it may
    /// back both <c>Session.HallId</c> and <c>Booth.HallId</c>). An admin may
    /// dedicate a hall to a single purpose (Booth / Session / Meeting) so the
    /// Control Panel surfaces the matching layout + allocation tools. A
    /// <c>MeetingTable</c> / business-meeting may only target a Meeting or General
    /// hall.</summary>
    public HallPurpose Purpose { get; set; } = HallPurpose.General;

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

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
