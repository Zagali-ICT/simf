namespace SIMF.Common.Enums;

/// <summary>D-199 (Mockup page 28 — "Audience comments" / تعليقات الجمهور) —
/// the moderation state of one audience comment on a live
/// <c>Session</c>.
///
/// <para>The mockup surfaces two visible states to the audience: a
/// "waiting" pill (في الانتظار) and an "answered" pill (مُجاب). The
/// moderation model behind those is three states: a comment arrives
/// <see cref="Pending"/>, a moderator (or the AI filter) moves it to
/// <see cref="Approved"/> so the audience sees it, or to
/// <see cref="Hidden"/> so it is retained for audit but never shown.
/// The mockup's "answered" badge is a separate display concern driven
/// by the speaker workflow — not a moderation state — so it is not
/// modelled here.</para>
///
/// <para><b>New enum (D-199):</b> the D-110 enum freeze is lifted for
/// the new event-module tables. Values are append-only from here:
/// renaming or reordering an existing value is a breaking wire change.</para>
/// </summary>
public enum SessionCommentStatus
{
    /// <summary>Submitted, not yet cleared for display. The default
    /// landing state when the AI filter is inconclusive or the
    /// deployment requires manual moderation.</summary>
    Pending = 0,

    /// <summary>Cleared for display — visible to the audience on the
    /// public comments feed. Set by the AI filter (auto-approve) or by
    /// a moderator.</summary>
    Approved = 1,

    /// <summary>Withheld from the audience (off-topic, abusive,
    /// spam). The row is retained for audit but never returned on the
    /// public feed.</summary>
    Hidden = 2,
}
