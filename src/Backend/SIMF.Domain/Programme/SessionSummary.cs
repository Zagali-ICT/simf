namespace SIMF.Domain.Programme;

/// <summary>
/// The Scientific Committee's minutes (محضر) for one <see cref="Session"/>, one
/// summary per session. The Committee drafts the four content sections, often
/// from an AI first draft, edits them, and publishes; the app shows the summary
/// only once <see cref="PublishedAt"/> is stamped.
///
/// <para>Publishing is the Committee's own editorial act, orthogonal to the
/// session's broadcast <see cref="Session.Status"/>: a session that has already
/// run may still have no minutes, and publishing minutes changes nothing about
/// the session.</para>
/// </summary>
public sealed class SessionSummary
{
    public Guid Id { get; set; }

    /// <summary>Unique: one summary per session. A real foreign key, the session
    /// being in the same DbContext, and cascade-deleted with it, a summary being
    /// meaningless without its session.</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    // The four content sections, under the headings the summary screen shows:
    // key points ("أبرز النقاط"), recommendations ("التوصيات"), speakers
    // ("المتحدثون") and the full text ("النص الكامل"). Each is bilingual, an
    // English property paired with an Arabic one, so the app can render either
    // with a fallback. KeyPoints is a bullet list stored as newline-delimited
    // text: one bullet per non-empty line.
    public string KeyPoints { get; set; } = string.Empty;

    public string KeyPointsArabic { get; set; } = string.Empty;

    public string Recommendations { get; set; } = string.Empty;

    public string RecommendationsArabic { get; set; } = string.Empty;

    /// <summary>Free text, and deliberately not a projection of the session's
    /// scheduled speakers: minutes record who actually took part, which can
    /// differ from the agenda.</summary>
    public string Speakers { get; set; } = string.Empty;

    public string SpeakersArabic { get; set; } = string.Empty;

    public string FullText { get; set; } = string.Empty;

    public string FullTextArabic { get; set; } = string.Empty;

    /// <summary>The model that produced the draft, or null when the summary was
    /// written by hand. Non-null is what raises the app's auto-generated banner.
    /// An edit never clears it: it records where the text started, not how much
    /// of the model's wording survives.</summary>
    public string? AiModel { get; set; }

    /// <summary>An optional short summary video the team produces, not the full
    /// recording on <see cref="Session.LiveStreamFileId"/>. The app shows it as a
    /// second player beside that one, and null hides that player. A row in
    /// <c>StoredFiles</c> like the session's own feed - an external link, held to
    /// the same classifier rule, and reaching the client verbatim.</summary>
    public Guid? SummaryVideoFileId { get; set; }

    // -- AI transparency ------------------------------------------------------

    /// <summary>The untouched output of the generate action, captured once and
    /// never overwritten by an edit, so the Control Panel can show the original
    /// model text beside the working copy a reviewer has been changing. Only the
    /// Arabic full text is drafted by the model, the prompt being written to
    /// produce Arabic minutes, so this mirrors <see cref="FullTextArabic"/> and
    /// nothing else. Null when no draft has been generated. Internal to the
    /// Control Panel: never projected onto a public or app contract.</summary>
    public string? AiDraftFullTextArabic { get; set; }

    /// <summary>When that snapshot was taken. A re-generate refreshes both
    /// together.</summary>
    public DateTime? AiDraftGeneratedAt { get; set; }

    /// <summary>Stamped on publish and cleared on un-publish, null while the
    /// summary is a draft. The app read requires a non-null value.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>A bare Guid rather than a navigation, as every user id on this
    /// entity is: these users live in the Identity database, so there is no
    /// foreign key across the two.</summary>
    public Guid? PublishedByUserId { get; set; }

    // -- Review and approval --------------------------------------------------
    // On top of the publish gate the minutes move Draft → InReview → Approved
    // before the session's host or moderator may read them. Each step is a
    // nullable timestamp and the status is derived from the furthest one
    // reached: ApprovedAt set means Approved, otherwise ReviewSubmittedAt set
    // means InReview, otherwise Draft.

    /// <summary>Stamped when a team member submits the draft for approval. Any
    /// content edit, or a return to draft, clears it.</summary>
    public DateTime? ReviewSubmittedAt { get; set; }

    public Guid? ReviewSubmittedByUserId { get; set; }

    /// <summary>Stamped when the team approves the minutes, which is what makes
    /// them readable by the session's host or moderator. Cleared by any content
    /// edit or a return to draft, so an edited summary must be approved
    /// again.</summary>
    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    /// <summary>Soft delete: clearing it hides the summary and keeps the
    /// row.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }
}
