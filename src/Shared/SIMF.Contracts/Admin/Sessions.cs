using SIMF.Common.Enums;

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
    DateTimeOffset CreatedAt,
    // B9b — D-226: appended (default) — the CP grid resolves the name client-side.
    Guid? CategoryId = null,
    // P3.2 — D-231: broadcast lifecycle status (appended, default Scheduled).
    SessionStatus Status = SessionStatus.Scheduled);

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
    DateTimeOffset? UpdatedAt,
    // B9b — D-226: the session's category (dynamic lookup); null when unset.
    Guid? CategoryId = null,
    // P3.2 — D-231: broadcast lifecycle (appended, defaults preserve the wire).
    SessionStatus Status = SessionStatus.Scheduled,
    DateTimeOffset? PublishedAt = null,
    // P3.2b — D-232: recording metadata (the bytes live out-of-row on disk).
    bool HasRecording = false,
    string? RecordingFileName = null,
    long? RecordingSizeBytes = null,
    DateTimeOffset? RecordingUploadedAt = null);

/// <summary>D-165 — one entry in <see cref="AdminSessionDetail.Speakers"/>.
/// Order matters: 0 = primary speaker for the session card.</summary>
public sealed record AdminSessionSpeakerEntry(
    Guid SpeakerId,
    string Name,
    string NameArabic,
    int DisplayOrder,
    // B9 — D-225: speaker/host role. Default keeps existing construction sites
    // (and old request payloads that omit it) compiling as plain speakers.
    SessionSpeakerRole Role = SessionSpeakerRole.Speaker);

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
    // B9b — D-226: optional session category (dynamic lookup).
    public Guid? CategoryId { get; set; }
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
    // B9b — D-226: optional session category (dynamic lookup).
    public Guid? CategoryId { get; set; }
    public IList<AdminSessionSpeakerEntry> Speakers { get; set; }
        = new List<AdminSessionSpeakerEntry>();
    public IList<Guid> ThemeIds { get; set; } = new List<Guid>();
    public bool IsActive { get; set; } = true;
}

/// <summary>P3.2 — D-231: the Committee's lifecycle transition. The service
/// enforces the allowed adjacent moves
/// (<c>Scheduled ↔ Held ↔ Recorded ↔ Published</c>); an illegal jump is a
/// 400. Setting the same status is an idempotent no-op.</summary>
public sealed class SetSessionStatusRequest
{
    public SessionStatus Status { get; set; }
}
