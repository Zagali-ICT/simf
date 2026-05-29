namespace SIMF.Contracts.Admin;

/// <summary>D-165 (gap doc G3, PDF §2.9) — one row in the admin Sessions
/// grid. Hall name (EN + AR) is projected alongside so the grid does
/// not need a second fetch.</summary>
public sealed record AdminSessionSummary(
    Guid Id,
    string Code,
    string Title,
    string TitleArabic,
    Guid HallId,
    string HallName,
    string HallNameArabic,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int Capacity,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>D-165 — full session detail (Details + Edit modals).
/// Includes the speaker and theme join sets so the editor can
/// pre-populate the multi-pickers without an extra round-trip.</summary>
public sealed record AdminSessionDetail(
    Guid Id,
    string Code,
    string Title,
    string TitleArabic,
    string? Description,
    string? DescriptionArabic,
    Guid HallId,
    string HallName,
    string HallNameArabic,
    int HallCapacity,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int? CapacityOverride,
    int EffectiveCapacity,
    bool IsActive,
    IReadOnlyList<AdminSessionSpeakerEntry> Speakers,
    IReadOnlyList<Guid> ThemeIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>D-165 — one entry in <see cref="AdminSessionDetail.Speakers"/>.
/// Order matters: 0 = primary speaker for the session card.</summary>
public sealed record AdminSessionSpeakerEntry(
    Guid SpeakerId,
    string Name,
    string NameArabic,
    int DisplayOrder);

public sealed class AdminCreateSessionRequest
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public Guid HallId { get; set; }
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public int? CapacityOverride { get; set; }
    public IList<AdminSessionSpeakerEntry> Speakers { get; set; }
        = new List<AdminSessionSpeakerEntry>();
    public IList<Guid> ThemeIds { get; set; } = new List<Guid>();
}

public sealed class AdminUpdateSessionRequest
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public Guid HallId { get; set; }
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public int? CapacityOverride { get; set; }
    public IList<AdminSessionSpeakerEntry> Speakers { get; set; }
        = new List<AdminSessionSpeakerEntry>();
    public IList<Guid> ThemeIds { get; set; } = new List<Guid>();
    public bool IsActive { get; set; } = true;
}
