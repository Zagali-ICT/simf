namespace SIMF.Domain.Programme;

/// <summary>The Scientific Committee's minutes (محضر) for one <see cref="Session"/>.
/// Publishing is the Committee's own editorial act, orthogonal to the session's
/// broadcast <see cref="Session.Status"/>.
///
/// <para>Deliberately does NOT derive <c>BaseAuditEntity</c>, though it carries
/// look-alike columns and names its actor <see cref="UpdatedByUserId"/> where the base
/// says <c>UpdatedBy</c>. The audit-stamping interceptor selects on that base type, so
/// deriving would start force-stamping a row the desk stamps by hand today, and would
/// add the <c>CreatedBy</c> and <c>DeletedAt</c> columns this row does not have. Two
/// behaviour changes, not a rename — so the divergence stays.</para></summary>
public sealed class SessionSummary
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>A bullet list stored as newline-delimited text: one bullet per non-empty line.</summary>
    public string KeyPoints { get; set; } = string.Empty;

    public string KeyPointsArabic { get; set; } = string.Empty;

    public string Recommendations { get; set; } = string.Empty;

    public string RecommendationsArabic { get; set; } = string.Empty;

    /// <summary>Free text, deliberately not a projection of the session's scheduled
    /// speakers: minutes record who actually took part.</summary>
    public string Speakers { get; set; } = string.Empty;

    public string SpeakersArabic { get; set; } = string.Empty;

    public string FullText { get; set; } = string.Empty;

    public string FullTextArabic { get; set; } = string.Empty;

    /// <summary>The model that produced the draft, null when written by hand. An edit never
    /// clears it: it records where the text started, not how much of it survives.</summary>
    public string? AiModel { get; set; }

    /// <summary>An optional short summary video the team produces, not the full recording.
    /// A <c>StoredFiles</c> row like the session's own feed.</summary>
    public Guid? SummaryVideoFileId { get; set; }

    /// <summary>The untouched generate output, captured once and never overwritten by an edit,
    /// so the Control Panel can show it beside the working copy. Only the Arabic full text is
    /// drafted by the model. Never projected onto a public or app contract.</summary>
    public string? AiDraftFullTextArabic { get; set; }

    public DateTime? AiDraftGeneratedAt { get; set; }

    /// <summary>Cleared on un-publish; the app read requires a non-null value.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Bare Guid, as every user id here is: these users live in the Identity
    /// database, so no key crosses the two.
    ///
    /// <para>This and its two siblings below are write-only today: the desk sets them
    /// on each transition and clears them again on reset / un-publish, but no contract
    /// projects them yet. They are kept deliberately, not left behind. The row-audit
    /// trail and the operation log already record who performed each transition, so
    /// these columns are redundant as HISTORY — what they hold that neither log holds
    /// cheaply is CURRENT STATE: who owns the sign-off that is standing right now, in
    /// one read of the row instead of a reconstruction from an append-only log.</para></summary>
    public Guid? PublishedByUserId { get; set; }

    // Status is derived from the furthest timestamp reached: ApprovedAt means Approved,
    // else ReviewSubmittedAt means InReview, else Draft.

    /// <summary>Stamped on submit for approval. Any content edit, or a return to draft, clears it.</summary>
    public DateTime? ReviewSubmittedAt { get; set; }

    public Guid? ReviewSubmittedByUserId { get; set; }

    /// <summary>Approval is what makes the minutes readable by the session's host or moderator.
    /// Cleared by any content edit, so an edited summary must be approved again.</summary>
    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    /// <summary>Constant true today: the desk has no delete or deactivate action, so
    /// nothing writes false, and a session's minutes are retracted by un-publishing
    /// rather than by removing the row. Every read still filters on it — the admin
    /// desk, the app read and the host read alike — so the day a deactivate lands the
    /// gates already hold. Kept as the defensive filter it is, not as a live flag.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }
}
