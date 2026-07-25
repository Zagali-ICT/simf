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
    DateTimeOffset Start,
    DateTimeOffset End,
    int Capacity,
    bool IsActive,
    DateTimeOffset CreatedAt,
    // B9b — D-226: appended (default) — the CP grid resolves the name client-side.
    Guid? CategoryId = null,
    // P3.2 — D-231: broadcast lifecycle status (appended, default Scheduled).
    SessionStatus Status = SessionStatus.Scheduled,
    // D-452 — session type (Workshop/Session/Event) for the app type tabs.
    SessionType? Type = null,
    // D-506 — carried so the grid Excel export can round-trip them (not rendered
    // as grid columns). Appended (defaulted) so the wire stays append-only; blank
    // when unset.
    string? Description = null,
    string? DescriptionArabic = null,
    string? LiveStreamUrl = null,
    string? LiveSignLanguageUrl = null,
    string? LiveCaptions = null,
    string? LiveCaptionsArabic = null,
    SeatSelectionMode? SeatSelectionModeOverride = null);

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
    DateTimeOffset Start,
    DateTimeOffset End,
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
    DateTimeOffset? RecordingUploadedAt = null,
    // §8 — live broadcast feed(s); null when the session is not live.
    string? LiveStreamUrl = null,
    string? LiveSignLanguageUrl = null,
    // P5 — D-439: AI live-caption text (manual stub provider, bilingual).
    string? LiveCaptions = null,
    string? LiveCaptionsArabic = null,
    // D-452 — session type (Workshop/Session/Event) for the app type tabs.
    SessionType? Type = null,
    // D-485 — per-session override of the hall's seat-selection mode; null =
    // inherit the hall. Appended (defaulted) so the wire stays append-only.
    SeatSelectionMode? SeatSelectionModeOverride = null,
    // Website Session-detail (Figma 5991-85840): the "at a glance" language label
    // + the "أبرز المخرجات" key-outcome bullets. Appended (defaulted) — wire stays
    // append-only.
    string? Language = null,
    string? LanguageArabic = null,
    IReadOnlyList<AdminSessionOutcomeEntry>? Outcomes = null);

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

/// <summary>One editable "key outcome" bullet on a session (the "أبرز المخرجات"
/// list, Figma 5991-85840). Bilingual text + display order. Backed by the
/// SessionOutcome table.</summary>
public sealed record AdminSessionOutcomeEntry(
    string Text,
    string TextArabic,
    int DisplayOrder);

public sealed class AdminCreateSessionRequest
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public Guid HallId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public int? CapacityOverride { get; set; }
    // B9b — D-226: optional session category (dynamic lookup).
    public Guid? CategoryId { get; set; }
    public IList<AdminSessionSpeakerEntry> Speakers { get; set; }
        = new List<AdminSessionSpeakerEntry>();
    public IList<Guid> ThemeIds { get; set; } = new List<Guid>();
    // §8 / D-349 — optional live feed URLs (YouTube POC + HLS fallback; the
    // shared LiveStreamUrlPolicy validates both).
    public string? LiveStreamUrl { get; set; }
    public string? LiveSignLanguageUrl { get; set; }
    // P5 — D-439 — optional AI live-caption text (manual stub provider, bilingual).
    public string? LiveCaptions { get; set; }
    public string? LiveCaptionsArabic { get; set; }
    // D-452 — session type (Workshop/Session/Event) for the app type tabs.
    public SessionType? Type { get; set; }
    // D-485 — optional per-session override of the hall's seat-selection mode.
    public SeatSelectionMode? SeatSelectionModeOverride { get; set; }
    // Website Session-detail (Figma 5991-85840): bilingual "at a glance" language
    // label + the "أبرز المخرجات" key-outcome bullets.
    public string? Language { get; set; }
    public string? LanguageArabic { get; set; }
    public IList<AdminSessionOutcomeEntry> Outcomes { get; set; }
        = new List<AdminSessionOutcomeEntry>();
}

public sealed class AdminUpdateSessionRequest
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public Guid HallId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public int? CapacityOverride { get; set; }
    // B9b — D-226: optional session category (dynamic lookup).
    public Guid? CategoryId { get; set; }
    public IList<AdminSessionSpeakerEntry> Speakers { get; set; }
        = new List<AdminSessionSpeakerEntry>();
    public IList<Guid> ThemeIds { get; set; } = new List<Guid>();
    public bool IsActive { get; set; } = true;
    // §8 / D-349 — optional live feed URLs (YouTube POC + HLS fallback; the
    // shared LiveStreamUrlPolicy validates both).
    public string? LiveStreamUrl { get; set; }
    public string? LiveSignLanguageUrl { get; set; }
    // P5 — D-439 — optional AI live-caption text (manual stub provider, bilingual).
    public string? LiveCaptions { get; set; }
    public string? LiveCaptionsArabic { get; set; }
    // D-452 — session type (Workshop/Session/Event) for the app type tabs.
    public SessionType? Type { get; set; }
    // D-485 — optional per-session override of the hall's seat-selection mode.
    public SeatSelectionMode? SeatSelectionModeOverride { get; set; }
    // Website Session-detail (Figma 5991-85840): bilingual "at a glance" language
    // label + the "أبرز المخرجات" key-outcome bullets.
    public string? Language { get; set; }
    public string? LanguageArabic { get; set; }
    public IList<AdminSessionOutcomeEntry> Outcomes { get; set; }
        = new List<AdminSessionOutcomeEntry>();
}

/// <summary>P3.2 — D-231: the Committee's lifecycle transition. The service
/// enforces the allowed adjacent moves
/// (<c>Scheduled ↔ Held ↔ Recorded ↔ Published</c>); an illegal jump is a
/// 400. Setting the same status is an idempotent no-op.</summary>
public sealed class SetSessionStatusRequest
{
    public SessionStatus Status { get; set; }
}
