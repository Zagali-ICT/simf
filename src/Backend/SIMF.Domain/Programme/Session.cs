using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>
/// D-165 (gap doc G3, PDF §2.9) — one scheduled run-of-show talk.
/// A Session is the time-window slot in a specific <see cref="Hall"/>
/// that one or more <see cref="Speaker"/>s present at, optionally
/// tagged with one or more <see cref="Theme"/>s for editorial
/// grouping in the agenda view.
///
/// <para><b>Distinct from <see cref="Theme"/>:</b> a Theme is a pillar
/// — an editorial category that colour-codes content. A Session is a
/// scheduled talk. Many Sessions can belong to one Theme; one Session
/// can be tagged with several Themes if it spans pillars.</para>
///
/// <para><b>Capacity:</b> defaults to the parent <see cref="Hall"/>'s
/// <c>SeatCount</c>. The PDF §2.9 requires per-session expansion or
/// contraction (the room can be reconfigured for one session); the
/// optional <see cref="CapacityOverride"/> column captures the
/// per-session value. Null means "use the hall default".</para>
/// </summary>
public class Session : BaseAuditEntity
{
    /// <summary>Stable admin code (2–16 chars; unique across all
    /// sessions). Used by the booking module + the URL of the public
    /// session page.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>English title shown on the agenda card.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Arabic title — paired with <see cref="Title"/>.</summary>
    public string TitleArabic { get; set; } = string.Empty;

    /// <summary>Optional English abstract / description (≤2048).</summary>
    public string? Description { get; set; }

    /// <summary>Optional Arabic abstract / description (≤2048).</summary>
    public string? DescriptionArabic { get; set; }

    /// <summary>The hosting hall. Real FK — same DbContext.</summary>
    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

    /// <summary>B9b — D-226 (FDS-004 §5.4): the session's category, an optional
    /// real FK to the dynamic <see cref="SessionCategory"/> lookup (same
    /// DbContext, OnDelete.Restrict). Null until a category is assigned.</summary>
    public Guid? CategoryId { get; set; }
    public SessionCategory? Category { get; set; }

    /// <summary>Session start (UTC). The Flutter agenda renders local-
    /// time per the user's tz.</summary>
    public DateTimeOffset StartUtc { get; set; }

    /// <summary>Session end (UTC). Must be > <see cref="StartUtc"/>;
    /// validated at the service layer.</summary>
    public DateTimeOffset EndUtc { get; set; }

    /// <summary>D-165 (PDF §2.9) — optional per-session override of the
    /// parent <see cref="Hall"/>'s <c>SeatCount</c>. Null means "use
    /// the hall default" — most sessions inherit; only the
    /// reconfigured rooms override.</summary>
    public int? CapacityOverride { get; set; }

    /// <summary>P1.7 (D-217 freeze-lift) — set by the automated reminder
    /// worker once it has dispatched the "starting soon" notifications for
    /// this session. The null check is the worker's dedup guard: a session
    /// is reminded exactly once. Null until reminded (the normal state).</summary>
    public DateTimeOffset? ReminderSentUtc { get; set; }

    /// <summary>P3.2 — D-231 (Completion Programme §5.2, Option A): the
    /// broadcast lifecycle. The Scientific Committee drives the transitions
    /// in the CP (<c>Scheduled → Held → Recorded → Published</c>).
    /// Orthogonal to <see cref="IsActive"/> (soft-delete). New sessions
    /// start <see cref="SessionStatus.Scheduled"/>.</summary>
    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;

    /// <summary>P3.2 — D-231: stamped when the session is published
    /// (<see cref="SessionStatus.Published"/>) and cleared if it is
    /// un-published. Null in every other state.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>P3.2b — D-232 (D-213): the recording attached to this session,
    /// stored out-of-row on the filesystem behind <c>ISessionRecordingStorage</c>.
    /// All null until a recording is uploaded; cleared when it is deleted.
    /// Orthogonal to <see cref="Status"/> — uploading does not change the
    /// lifecycle, and the public stream requires both a recording AND
    /// <see cref="SessionStatus.Published"/>.</summary>
    public string? RecordingStoredFileName { get; set; }

    /// <summary>The original upload file name (shown in the CP, used as the
    /// download name).</summary>
    public string? RecordingFileName { get; set; }

    /// <summary>The recording's MIME type (e.g. <c>video/mp4</c>) — echoed on
    /// the stream response so the player knows how to decode it.</summary>
    public string? RecordingContentType { get; set; }

    /// <summary>The recording's size in bytes (shown in the CP).</summary>
    public long? RecordingSizeBytes { get; set; }

    /// <summary>When the recording was last uploaded.</summary>
    public DateTimeOffset? RecordingUploadedAt { get; set; }

    /// <summary>Bare <c>Guid</c> of the admin who uploaded the recording —
    /// cross-context (Identity DB), no FK (D-157).</summary>
    public Guid? RecordingUploadedByUserId { get; set; }

    /// <summary>§8 (Mockup screen 25 "البث المباشر") — the LIVE broadcast stream
    /// URL, set manually by an admin in the CP. Non-null
    /// = this session has a live broadcast (the app shows the LIVE player);
    /// null = recorded/scheduled only. D-349: the provider is YouTube (a YouTube
    /// watch/live URL) for the POC, with a direct HLS/MP4 URL accepted as a
    /// fallback — both validated by <c>LiveStreamUrlPolicy</c>. Orthogonal
    /// to <see cref="Status"/> and the recording.</summary>
    public string? LiveStreamUrl { get; set; }

    /// <summary>§8 (Mockup screen 26 "لغة الإشارة") — the optional alternate
    /// live stream that carries sign-language interpretation. Non-null = the app
    /// shows the sign-language toggle on the live player; null = no sign-language
    /// feed.</summary>
    public string? LiveSignLanguageUrl { get; set; }

    /// <summary>P5 — D-439 (Mockup screen 25 "البث المباشر", Figma 934:3613): the
    /// AI live-caption / running-transcript line shown under the player ("الترجمة
    /// الفورية للنص المنطوق"). Non-null = the app renders the caption strip with
    /// this text; null = the strip shows the placeholder hint (and YouTube CC
    /// supplies captions for a YouTube feed). <b>Provider stubbed (D-439):</b> like
    /// <see cref="LiveStreamUrl"/> (a manually-entered YouTube URL, not a
    /// streaming-infra integration), the caption text is entered by an admin in
    /// the CP for the POC; the future <c>ILiveCaptionProvider</c> speech-to-text
    /// seam would auto-populate this same column. English line.</summary>
    public string? LiveCaptions { get; set; }

    /// <summary>P5 — D-439: the Arabic pair of <see cref="LiveCaptions"/>. The app
    /// renders the active locale with a fallback to the other when one is blank.</summary>
    public string? LiveCaptionsArabic { get; set; }

    /// <summary>M-to-M with <see cref="Speaker"/> via the explicit join
    /// entity <see cref="SessionSpeaker"/>. Explicit because the
    /// composite key + the per-row metadata (DisplayOrder) earns the
    /// extra type; mirrors the D-161 reasoning for GateProfileTypeAllow.</summary>
    public ICollection<SessionSpeaker> Speakers { get; set; }
        = new List<SessionSpeaker>();

    /// <summary>M-to-M with <see cref="Theme"/> via <see cref="SessionTheme"/>.
    /// A session belongs to zero or more themes; the agenda groups by
    /// the first-listed theme as the "primary pillar".</summary>
    public ICollection<SessionTheme> Themes { get; set; }
        = new List<SessionTheme>();
}

/// <summary>D-165 — join row linking a <see cref="Session"/> to one
/// <see cref="Speaker"/>. Composite PK (SessionId, SpeakerId).
/// <see cref="DisplayOrder"/> drives the speaker-list order on the
/// session card (primary speaker first, supporting speakers after).</summary>
public class SessionSpeaker
{
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    public Guid SpeakerId { get; set; }
    public Speaker? Speaker { get; set; }

    /// <summary>Order of the speaker in the session's speaker list
    /// (0 = primary).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>B9 — D-225 (FDS-004 §5.4): this person's role in THIS session —
    /// speaker or host. Defaults to <see cref="SessionSpeakerRole.Speaker"/>.</summary>
    public SessionSpeakerRole Role { get; set; }
}

/// <summary>D-165 — join row linking a <see cref="Session"/> to one
/// <see cref="Theme"/> (composite PK). One session can carry several
/// themes — useful for the cross-pillar "Cybersecurity × Maritime
/// Navigation" sessions where the agenda should show the card under
/// both pillar groups.</summary>
public class SessionTheme
{
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    public Guid ThemeId { get; set; }
    public Theme? Theme { get; set; }
}
