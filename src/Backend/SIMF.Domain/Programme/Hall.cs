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

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
