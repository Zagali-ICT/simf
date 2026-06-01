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
public class Session
{
    public Guid Id { get; set; }

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

    /// <summary>Soft-delete flag — hides the session from public
    /// agenda views without losing the row.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>P1.7 (D-217 freeze-lift) — set by the automated reminder
    /// worker once it has dispatched the "starting soon" notifications for
    /// this session. The null check is the worker's dedup guard: a session
    /// is reminded exactly once. Null until reminded (the normal state).</summary>
    public DateTimeOffset? ReminderSentUtc { get; set; }

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
