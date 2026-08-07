using SIMF.Common.Enums;
using SIMF.Common.Options;

namespace SIMF.Contracts.Admin;

/// <summary>One row in the admin Sessions grid. Hall name (EN + AR) is
/// projected alongside so the grid does not need a second fetch.</summary>
public sealed record AdminSessionSummary(
    Guid Id,
    string Code,
    string Title,
    string TitleArabic,
    Guid HallId,
    string HallName,
    string HallNameArabic,
    DateTime Start,
    DateTime End,
    int Capacity,
    bool IsActive,
    DateTime CreatedAt,
    // Appended (default) — the CP grid resolves the name client-side.
    Guid? CategoryId = null,
    // Broadcast lifecycle status (appended, default Scheduled).
    SessionStatus Status = SessionStatus.Scheduled,
    // Session type (Workshop/Session/Event) for the app type tabs.
    SessionType? Type = null,
    // Carried so the grid Excel export can round-trip them (not rendered
    // as grid columns). Appended (defaulted) so the wire stays append-only; blank
    // when unset.
    string? Description = null,
    string? DescriptionArabic = null,
    string? LiveStreamUrl = null,
    string? LiveSignLanguageUrl = null,
    string? LiveCaptions = null,
    string? LiveCaptionsArabic = null,
    SeatSelectionMode? SeatSelectionModeOverride = null,
    // Carried for the same Excel round-trip reason as the live
    // fields above: without it, an admin who authors notices on 40 sessions, then
    // exports the grid to bulk-edit start times and re-imports, silently loses
    // every notice. The Excel lane is the only bulk authoring path there is.
    string? LiveNotice = null,
    string? LiveNoticeArabic = null,
    // The session's own arrival-grace override in minutes; null = inherit
    // the hall. Carried for the same Excel round-trip reason as the fields
    // above.
    int? ArrivalGraceMinutesOverride = null,
    // What the server will ACTUALLY use for this session, already
    // resolved (override -> hall -> global -> 15). The Hall-Arrivals console
    // reads this instead of its own hard-coded 15, which could see neither of the
    // two layers above and so disagreed with the server about which sessions are
    // open for arrivals.
    int EffectiveArrivalGraceMinutes = WalkInModeOptions.DefaultArrivalGraceMinutes);

/// <summary>Full session detail (Details + Edit modals).
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
    DateTime Start,
    DateTime End,
    int? CapacityOverride,
    int EffectiveCapacity,
    bool IsActive,
    IReadOnlyList<AdminSessionSpeakerEntry> Speakers,
    IReadOnlyList<Guid> ThemeIds,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // The session's category (dynamic lookup); null when unset.
    Guid? CategoryId = null,
    // Broadcast lifecycle (appended, defaults preserve the wire).
    SessionStatus Status = SessionStatus.Scheduled,
    DateTime? PublishedAt = null,
    // Recording metadata (the bytes live out-of-row on disk).
    bool HasRecording = false,
    string? RecordingFileName = null,
    long? RecordingSizeBytes = null,
    DateTime? RecordingUploadedAt = null,
    // Live broadcast feed(s); null when the session is not live.
    string? LiveStreamUrl = null,
    string? LiveSignLanguageUrl = null,
    // AI live-caption text (manual stub provider, bilingual).
    string? LiveCaptions = null,
    string? LiveCaptionsArabic = null,
    // Session type (Workshop/Session/Event) for the app type tabs.
    SessionType? Type = null,
    // Per-session override of the hall's seat-selection mode; null =
    // inherit the hall. Appended (defaulted) so the wire stays append-only.
    SeatSelectionMode? SeatSelectionModeOverride = null,
    // Website Session-detail (Figma 5991-85840): the "at a glance" language label
    // + the "أبرز المخرجات" key-outcome bullets. Appended (defaulted) — wire stays
    // append-only.
    string? Language = null,
    string? LanguageArabic = null,
    IReadOnlyList<AdminSessionOutcomeEntry>? Outcomes = null,
    // The seat-release blast radius of a hall / start / end change.
    // Appended (defaulted) so the wire stays append-only.
    //
    // The first pair is the CURRENT holding, stamped on every read: how many
    // active visitor registrations and admin row-blocks a hall-or-time change
    // would destroy. The Control Panel reads them off the loaded detail so the
    // edit form can warn with a real number BEFORE it submits — no extra
    // endpoint, no extra permission.
    int ActiveReservationCount = 0,
    int ActiveAdminBlockCount = 0,
    // The second pair is what the update JUST destroyed — non-zero only on the
    // response of an update that actually moved the hall or the time window, so
    // the Control Panel can report the outcome instead of a bare "was updated".
    int ReleasedReservationCount = 0,
    int ReleasedAdminBlockCount = 0,
    // The bilingual informational notice shown
    // WITH the live stream. Purely informational: it gates nothing, and the feed
    // plays for every caller. Carried on the detail so the CP edit form can read the
    // stored value back into its inputs. Appended (defaulted) — wire stays
    // append-only.
    string? LiveNotice = null,
    string? LiveNoticeArabic = null,
    // The per-session arrival-grace override (null = inherit the hall),
    // and the number clearing that override would fall back to: the hall's grace,
    // else the global value. Deliberately NOT the resolved-including-override
    // value — the edit form offers this as "leave blank to get this", and the
    // resolved one would tell an admin who HAS an override that clearing it
    // changes nothing.
    int? ArrivalGraceMinutesOverride = null,
    int InheritedArrivalGraceMinutes = WalkInModeOptions.DefaultArrivalGraceMinutes);

/// <summary>One entry in <see cref="AdminSessionDetail.Speakers"/>.
/// Order matters: 0 = primary speaker for the session card.</summary>
public sealed record AdminSessionSpeakerEntry(
    Guid SpeakerId,
    string Name,
    string NameArabic,
    int DisplayOrder,
    // Speaker/host role. Default keeps existing construction sites
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
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int? CapacityOverride { get; set; }
    // Optional session category (dynamic lookup).
    public Guid? CategoryId { get; set; }
    public IList<AdminSessionSpeakerEntry> Speakers { get; set; }
        = new List<AdminSessionSpeakerEntry>();
    public IList<Guid> ThemeIds { get; set; } = new List<Guid>();
    // Optional live feed URLs (YouTube POC + HLS fallback; the
    // shared LiveStreamUrlPolicy validates both).
    public string? LiveStreamUrl { get; set; }
    public string? LiveSignLanguageUrl { get; set; }
    // Optional AI live-caption text (manual stub provider, bilingual).
    public string? LiveCaptions { get; set; }
    public string? LiveCaptionsArabic { get; set; }
    // Optional bilingual notice shown WITH the live
    // stream. Informational only: it restricts nothing and withholds nothing.
    public string? LiveNotice { get; set; }
    public string? LiveNoticeArabic { get; set; }
    // Session type (Workshop/Session/Event) for the app type tabs.
    public SessionType? Type { get; set; }
    // Optional per-session override of the hall's seat-selection mode.
    public SeatSelectionMode? SeatSelectionModeOverride { get; set; }
    // Optional per-session override of the hall's arrival grace, in
    // minutes (0..240). Null = inherit the hall (which may itself inherit the
    // global value).
    public int? ArrivalGraceMinutesOverride { get; set; }
    // Website Session-detail (Figma 5991-85840): bilingual "at a glance" language
    // label + the "أبرز المخرجات" key-outcome bullets.
    public string? Language { get; set; }
    public string? LanguageArabic { get; set; }
    public IList<AdminSessionOutcomeEntry> Outcomes { get; set; }
        = new List<AdminSessionOutcomeEntry>();
}

/// <remarks>Not sealed: the admin update endpoint binds {id}+body via a derived
/// route class so it cannot drop a field at bind time.</remarks>
public class AdminUpdateSessionRequest
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public Guid HallId { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int? CapacityOverride { get; set; }
    // Optional session category (dynamic lookup).
    public Guid? CategoryId { get; set; }
    public IList<AdminSessionSpeakerEntry> Speakers { get; set; }
        = new List<AdminSessionSpeakerEntry>();
    public IList<Guid> ThemeIds { get; set; } = new List<Guid>();
    public bool IsActive { get; set; } = true;
    // Optional live feed URLs (YouTube POC + HLS fallback; the
    // shared LiveStreamUrlPolicy validates both).
    public string? LiveStreamUrl { get; set; }
    public string? LiveSignLanguageUrl { get; set; }
    // Optional AI live-caption text (manual stub provider, bilingual).
    public string? LiveCaptions { get; set; }
    public string? LiveCaptionsArabic { get; set; }
    // Optional bilingual notice shown WITH the live
    // stream. Informational only: it restricts nothing and withholds nothing.
    public string? LiveNotice { get; set; }
    public string? LiveNoticeArabic { get; set; }
    // Session type (Workshop/Session/Event) for the app type tabs.
    public SessionType? Type { get; set; }
    // Optional per-session override of the hall's seat-selection mode.
    public SeatSelectionMode? SeatSelectionModeOverride { get; set; }
    // Optional per-session override of the hall's arrival grace, in
    // minutes (0..240). Null = inherit the hall (which may itself inherit the
    // global value).
    public int? ArrivalGraceMinutesOverride { get; set; }
    // Website Session-detail (Figma 5991-85840): bilingual "at a glance" language
    // label + the "أبرز المخرجات" key-outcome bullets.
    public string? Language { get; set; }
    public string? LanguageArabic { get; set; }
    public IList<AdminSessionOutcomeEntry> Outcomes { get; set; }
        = new List<AdminSessionOutcomeEntry>();
}

/// <summary>The Committee's lifecycle transition. The service
/// enforces the allowed adjacent moves
/// (<c>Scheduled ↔ Held ↔ Recorded ↔ Published</c>); an illegal jump is a
/// 400. Setting the same status is an idempotent no-op.</summary>
public sealed class SetSessionStatusRequest
{
    public SessionStatus Status { get; set; }
}
