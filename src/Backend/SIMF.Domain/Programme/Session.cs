using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>
/// One scheduled talk: a time window in a <see cref="Hall"/>, presented by one or
/// more <see cref="Speaker"/>s and optionally tagged with <see cref="Theme"/>s.
///
/// <para>A <see cref="Theme"/> is an editorial pillar that groups content; a
/// Session is the talk itself. Many sessions share a theme, and a session that
/// spans pillars carries several.</para>
/// </summary>
public class Session : BaseAuditEntity
{
    /// <summary>Unique across all sessions, and the session's public URL segment.</summary>
    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }

    /// <summary>Free text such as "Arabic &amp; English". Null omits the language
    /// row from the public "at a glance" card.</summary>
    public string? Language { get; set; }
    public string? LanguageArabic { get; set; }

    /// <summary>A real foreign key: the hall lives in the same DbContext.</summary>
    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

    public Guid? CategoryId { get; set; }
    public SessionCategory? Category { get; set; }

    /// <summary>Drives the app's Workshop / Session / Event filter tabs. An
    /// untyped session appears only under "All".</summary>
    public SessionType? Type { get; set; }

    /// <summary>Saudi local time.</summary>
    public DateTime Start { get; set; }

    /// <summary>Saudi local time. Must be after <see cref="Start"/>, enforced by
    /// a database check constraint and again by the admin service.</summary>
    public DateTime End { get; set; }

    /// <summary>Null inherits the hall's seat count. Only a room reconfigured for
    /// one session overrides it.</summary>
    public int? CapacityOverride { get; set; }

    /// <summary>Null inherits the hall. Lets a single session run open-seating in
    /// an assigned-seat hall, or the reverse, without touching the hall.</summary>
    public SeatSelectionMode? SeatSelectionModeOverride { get; set; }

    /// <summary>Minutes (0..240) before the start that arrivals are accepted.
    /// Resolved session, then hall, then the global WalkInMode setting, then 15 —
    /// so one keynote can open early without changing the hall every other
    /// session that day runs in. Null inherits.</summary>
    public int? ArrivalGraceMinutesOverride { get; set; }

    /// <summary>Stamped by the reminder worker. The null check is its once-only
    /// guard, so a session is reminded exactly once.</summary>
    public DateTime? ReminderSent { get; set; }

    /// <summary>Stamped by the rating-prompt worker, and its once-only guard in
    /// the same way as <see cref="ReminderSent"/>.</summary>
    public DateTime? RatingPromptSent { get; set; }

    /// <summary>The broadcast lifecycle, driven by the Scientific Committee in the
    /// Control Panel. Orthogonal to <see cref="BaseAuditEntity.IsActive"/>, which
    /// is the soft-delete flag.</summary>
    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;

    /// <summary>Stamped on publish and cleared on un-publish; null otherwise.</summary>
    public DateTime? PublishedAt { get; set; }

    // The recording lives in the unified file store, so these columns hold only
    // its metadata plus the key. All null until something is uploaded, and all
    // cleared on delete. Orthogonal to Status: the public stream needs a
    // recording AND a Published session.
    //
    // RecordingFileId is a real foreign key into StoredFiles, and doubles as the
    // "has recording" sentinel that gates both the lifecycle transition to
    // Recorded/Published and the public stream.
    public Guid? RecordingFileId { get; set; }
    public string? RecordingFileName { get; set; }
    public string? RecordingContentType { get; set; }
    public long? RecordingSizeBytes { get; set; }
    public DateTime? RecordingUploadedAt { get; set; }

    /// <summary>A bare Guid rather than a navigation: the user lives in the
    /// Identity database, so there is no foreign key across the two.</summary>
    public Guid? RecordingUploadedByUserId { get; set; }

    /// <summary>The session's live broadcast feed, as a row in
    /// <c>StoredFiles</c>: non-null means the app shows the live player.
    ///
    /// <para>SIMF does not host the broadcast, so the row is an external link
    /// rather than bytes - but it is a file-store row all the same, which is what
    /// gives a feed a media type, a service policy, a sensitivity tier and an
    /// owner. As free text it had none of those, and nothing could join on it.
    /// The URL still reaches the clients verbatim: they classify it themselves to
    /// choose a YouTube player over a direct one, so serving it behind a redirect
    /// would break the very branch it feeds.</para></summary>
    public Guid? LiveStreamFileId { get; set; }

    /// <summary>The optional parallel feed carrying sign-language interpretation,
    /// stored the same way as <see cref="LiveStreamFileId"/>. Non-null adds the
    /// sign-language toggle to the live player.</summary>
    public Guid? LiveSignLanguageFileId { get; set; }

    /// <summary>The running-transcript line under the player. Typed by an admin,
    /// not generated: there is no speech-to-text integration, and a YouTube feed
    /// supplies its own captions. Null shows the placeholder hint instead.</summary>
    public string? LiveCaptions { get; set; }
    public string? LiveCaptionsArabic { get; set; }

    /// <summary><b>Informational only — this restricts nothing.</b> It was
    /// specified as a regional restriction on the live feed and the owner reversed
    /// that, so there is no region check and no location lookup anywhere; the feed
    /// plays for every caller and this text only adds a banner beside it. Blank on
    /// both languages shows no notice.</summary>
    public string? LiveNotice { get; set; }
    public string? LiveNoticeArabic { get; set; }

    /// <summary>Explicit join entity, because the join row carries its own
    /// <see cref="SessionSpeaker.DisplayOrder"/> and <see cref="SessionSpeaker.Role"/>.</summary>
    public ICollection<SessionSpeaker> Speakers { get; set; }
        = new List<SessionSpeaker>();

    public ICollection<SessionTheme> Themes { get; set; }
        = new List<SessionTheme>();

    /// <summary>The key-outcome bullets on the public session page, ordered by
    /// <see cref="SessionOutcome.DisplayOrder"/>. Cascade-deleted with the session.</summary>
    public ICollection<SessionOutcome> Outcomes { get; set; }
        = new List<SessionOutcome>();
}

/// <summary>Join row for <see cref="Session"/> and <see cref="Speaker"/>.
/// Composite key (SessionId, SpeakerId).</summary>
public class SessionSpeaker
{
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    public Guid SpeakerId { get; set; }
    public Speaker? Speaker { get; set; }

    /// <summary>Position in the session's speaker list; 0 is the primary speaker.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Whether this person speaks at or hosts this particular session.</summary>
    public SessionSpeakerRole Role { get; set; }
}

/// <summary>Join row for <see cref="Session"/> and <see cref="Theme"/>. A session
/// carrying several themes shows on the agenda under each of their pillar
/// groups.</summary>
public class SessionTheme
{
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    public Guid ThemeId { get; set; }
    public Theme? Theme { get; set; }
}
