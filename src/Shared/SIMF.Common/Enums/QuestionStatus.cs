namespace SIMF.Common.Enums;

/// <summary>
/// The 3-stage Q&amp;A pipeline state of
/// an audience question. A question is <see cref="Pending"/> until the
/// Scientific Committee acts on it: approve (→ <see cref="Approved"/>, the set
/// the per-session moderator desk then shows) or hide (→ <see cref="Hidden"/>).
/// The AI filter is advisory only and never changes this. Int-backed,
/// additive-only discipline (never renumber/rename an existing case).
/// </summary>
public enum QuestionStatus
{
    /// <summary>Awaiting the Scientific Committee (the default for a new
    /// submission).</summary>
    Pending = 0,

    /// <summary>Approved by the Committee — visible to the per-session
    /// moderator desk for the live push/reorder.</summary>
    Approved = 1,

    /// <summary>Hidden by the Committee or the moderator — retained for audit
    /// but never displayed.</summary>
    Hidden = 2,

    /// <summary>The per-session moderator marked the question
    /// ANSWERED on stage. A terminal desk state reached only from
    /// <see cref="Approved"/> (and reversible back to it): the question stays on
    /// the moderator desk under the "تمت الإجابة" tab instead of living in a
    /// screen-local set that died on screen exit. Appended value — the column is
    /// a plain <c>int</c> with no check constraint, so no migration is needed.</summary>
    Answered = 3,
}
