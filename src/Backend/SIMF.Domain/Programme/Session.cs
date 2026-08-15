using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Programme;

/// <summary>One scheduled talk: a time window in a <see cref="Hall"/>, presented
/// by <see cref="Speaker"/>s and optionally tagged with <see cref="Theme"/>s.</summary>
public class Session : BaseAuditEntity
{
    /// <summary>Also the session's public URL segment.</summary>
    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }

    public string? Language { get; set; }
    public string? LanguageArabic { get; set; }

    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

    public Guid? CategoryId { get; set; }
    public SessionCategory? Category { get; set; }

    /// <summary>Drives the app's filter tabs; an untyped session appears only under "All".</summary>
    public SessionType? Type { get; set; }

    /// <summary>Saudi local time.</summary>
    public DateTime Start { get; set; }

    /// <summary>Saudi local time.</summary>
    public DateTime End { get; set; }

    /// <summary>Null inherits the hall's seat count.</summary>
    public int? CapacityOverride { get; set; }

    /// <summary>Null inherits the hall's mode.</summary>
    public SeatSelectionMode? SeatSelectionModeOverride { get; set; }

    /// <summary>Resolved session, then hall, then the WalkInMode setting, then 15.</summary>
    public int? ArrivalGraceMinutesOverride { get; set; }

    /// <summary>Stamped by the reminder worker; the null check is its once-only guard.</summary>
    public DateTime? ReminderSent { get; set; }

    public DateTime? RatingPromptSent { get; set; }

    /// <summary>Broadcast lifecycle; orthogonal to the
    /// <see cref="BaseAuditEntity.IsActive"/> soft-delete flag.</summary>
    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;

    public DateTime? PublishedAt { get; set; }

    // RecordingFileId doubles as the "has recording" sentinel gating both the
    // transition to Recorded/Published and the public stream.
    public Guid? RecordingFileId { get; set; }
    public string? RecordingFileName { get; set; }
    public string? RecordingContentType { get; set; }
    public long? RecordingSizeBytes { get; set; }
    public DateTime? RecordingUploadedAt { get; set; }

    /// <summary>Bare Guid: the user lives in the Identity database, so no key crosses the two.</summary>
    public Guid? RecordingUploadedByUserId { get; set; }

    /// <summary>An external link held as a <c>StoredFiles</c> row, which gives the feed a media
    /// type, a policy and an owner. The URL must reach clients verbatim - they classify it to
    /// choose a YouTube player over a direct one, so a redirect would break that branch.</summary>
    public Guid? LiveStreamFileId { get; set; }

    /// <summary>Optional parallel sign-language feed, stored like <see cref="LiveStreamFileId"/>.</summary>
    public Guid? LiveSignLanguageFileId { get; set; }

    /// <summary>Typed by an admin, not generated: there is no speech-to-text integration.</summary>
    public string? LiveCaptions { get; set; }
    public string? LiveCaptionsArabic { get; set; }

    /// <summary>Informational banner only - it restricts nothing. There is no region check and
    /// no location lookup anywhere; the feed plays for every caller.</summary>
    public string? LiveNotice { get; set; }
    public string? LiveNoticeArabic { get; set; }

    public ICollection<SessionSpeaker> Speakers { get; set; }
        = new List<SessionSpeaker>();

    public ICollection<SessionTheme> Themes { get; set; }
        = new List<SessionTheme>();

    public ICollection<SessionOutcome> Outcomes { get; set; }
        = new List<SessionOutcome>();
}

public class SessionSpeaker
{
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    public Guid SpeakerId { get; set; }
    public Speaker? Speaker { get; set; }

    /// <summary>0 is the primary speaker.</summary>
    public int DisplayOrder { get; set; }

    public SessionSpeakerRole Role { get; set; }
}

public class SessionTheme
{
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    public Guid ThemeId { get; set; }
    public Theme? Theme { get; set; }
}
