namespace SIMF.Domain.Programme;

/// <summary>
/// P4.1 — D-237 (Completion Programme §6.4.1, Mockup screen 34 "ملخص الجلسة
/// بالذكاء الاصطناعي"): the AI session summary / محضر for one
/// <see cref="Session"/>. One summary per session (1:1, unique
/// <see cref="SessionId"/>). The Scientific Committee drafts it — optionally
/// AI-assisted (the draft is generated through the central AI provider seam in
/// the committee workflow, P4.1b) — reviews and edits the four content
/// sections, then publishes it. The app reads it on screen 34 only once
/// <see cref="PublishedAt"/> is stamped.
///
/// <para><b>Content sections</b> mirror the mockup: key points (a newline-
/// delimited bullet list — the app renders one bullet per non-empty line),
/// recommendations, speakers, and the full text behind the "النص الكامل"
/// button. Every section is bilingual (English + Arabic), matching the
/// <see cref="Session.Title"/>/<see cref="Session.TitleArabic"/> convention so
/// the app can show either language with a fallback.</para>
///
/// <para><b>Publish gate:</b> <see cref="PublishedAt"/> is the timestamp
/// convention (the News D-199 shape). Null = a draft the Committee is still
/// editing; non-null = published and readable by the app. Orthogonal to the
/// session's broadcast <see cref="Session.Status"/> — the محضر is the
/// Committee's own editorial document and is published by its own action.</para>
/// </summary>
public sealed class SessionSummary
{
    public Guid Id { get; set; }

    /// <summary>The session this summary belongs to. Real FK (same DbContext),
    /// unique — one summary per session — and cascade-deleted with its session
    /// (a summary is meaningless without it).</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>"أبرز النقاط" — newline-delimited bullet points (English). Each
    /// non-empty line is one bullet on screen 34.</summary>
    public string KeyPoints { get; set; } = string.Empty;

    /// <summary>"أبرز النقاط" — Arabic, paired with <see cref="KeyPoints"/>.</summary>
    public string KeyPointsArabic { get; set; } = string.Empty;

    /// <summary>"التوصيات" — recommendations paragraph (English).</summary>
    public string Recommendations { get; set; } = string.Empty;

    /// <summary>"التوصيات" — Arabic, paired with <see cref="Recommendations"/>.</summary>
    public string RecommendationsArabic { get; set; } = string.Empty;

    /// <summary>"المتحدثون" — the participants line as curated by the Committee
    /// for the minutes (English). Free text rather than a projection of the
    /// session's scheduled speakers: a محضر records who actually took part,
    /// which can differ from the agenda.</summary>
    public string Speakers { get; set; } = string.Empty;

    /// <summary>"المتحدثون" — Arabic, paired with <see cref="Speakers"/>.</summary>
    public string SpeakersArabic { get; set; } = string.Empty;

    /// <summary>"النص الكامل" — the full minutes text (English).</summary>
    public string FullText { get; set; } = string.Empty;

    /// <summary>"النص الكامل" — Arabic, paired with <see cref="FullText"/>.</summary>
    public string FullTextArabic { get; set; } = string.Empty;

    /// <summary>The AI model that drafted this summary (e.g. <c>echo</c>), or
    /// null when it was written by hand. Drives the app's "تم توليد ملخص
    /// تلقائي" (auto-generated) banner. Set by the generate action in P4.1b;
    /// editing never clears it (the draft origin is provenance, not state).</summary>
    public string? AiModel { get; set; }

    // -- Slice D (2026-07-19) — AI transparency --------------------------------

    /// <summary>The pristine AI draft: the untouched output the generate action
    /// produced, captured once at generation and NEVER overwritten by a Committee
    /// edit. Lets the CP editor show the original AI text beside the (possibly
    /// edited) working copy so a reviewer sees exactly what the model wrote. Only
    /// the Arabic full-text is AI-drafted (the seeded prompt writes Arabic
    /// minutes), so this mirrors <see cref="FullTextArabic"/>. Null when no AI
    /// draft has been generated (a hand-written summary, or a row predating this
    /// column). CP-internal — never projected onto any public/app contract.</summary>
    public string? AiDraftFullTextArabic { get; set; }

    /// <summary>When the pristine <see cref="AiDraftFullTextArabic"/> snapshot was
    /// last captured; each generate / re-generate refreshes both. Null when no AI
    /// draft has been generated.</summary>
    public DateTimeOffset? AiDraftGeneratedAt { get; set; }

    /// <summary>Stamped when the Committee publishes the summary; cleared when
    /// it is un-published. Null while it is a draft. The app read requires a
    /// non-null value.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Bare <c>Guid</c> of the admin who published — cross-context
    /// (Identity DB), no FK (D-157). Null while unpublished.</summary>
    public Guid? PublishedByUserId { get; set; }

    // -- D-472 (requirement #9) — the team review/approval workflow ------------
    // On top of the publish gate, the محضر moves Draft → InReview → Approved
    // before it is "ready for المحاور" (the session host / moderator). Each step
    // is a nullable timestamp, matching the PublishedAt convention. Derived
    // status: ApprovedAt != null → Approved; else ReviewSubmittedAt != null →
    // InReview; else Draft.

    /// <summary>Stamped when a team member submits the draft for approval
    /// (Draft → InReview). Cleared by any content edit or a return-to-draft.</summary>
    public DateTimeOffset? ReviewSubmittedAt { get; set; }

    /// <summary>Bare <c>Guid</c> of the team member who submitted it for review —
    /// cross-context (Identity DB), no FK (D-157).</summary>
    public Guid? ReviewSubmittedByUserId { get; set; }

    /// <summary>Stamped when the team approves the summary (InReview → Approved);
    /// the approved محضر then becomes readable by the session's host / moderator
    /// ("ready for المحاور"). Cleared by any content edit or a return-to-draft.</summary>
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>Bare <c>Guid</c> of the team member who approved — cross-context
    /// (Identity DB), no FK (D-157).</summary>
    public Guid? ApprovedByUserId { get; set; }

    /// <summary>Soft-delete flag — hides the summary without losing the row.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Bare <c>Guid</c> of the admin who last edited — cross-context,
    /// no FK (D-157).</summary>
    public Guid? UpdatedByUserId { get; set; }
}
