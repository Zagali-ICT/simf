namespace SIMF.Infrastructure.SessionComments;

/// <summary>D-199 — options for the audience-comment AI filter stub.
/// Bound from the <c>SessionComments:AiFilter</c> configuration section
/// (CLAUDE.md §18 — typed Options, no scattered magic-string keys).
///
/// <para>Until the real AI moderation call is wired (behind
/// <c>ICommentAiFilter</c>), <see cref="AutoApprove"/> controls the
/// stub's landing decision: <c>true</c> (the dev/mockup default — the
/// mockup shows comments appearing in the feed immediately) lands every
/// comment as Approved; <c>false</c> lands every comment as Pending so
/// a human moderator clears it. A deployment that wants
/// approve-before-display sets this to <c>false</c>.</para>
/// </summary>
public sealed class CommentAiFilterOptions
{
    public const string SectionName = "SessionComments:AiFilter";

    /// <summary>When true (default), the stub auto-approves every
    /// comment (landing status = Approved). When false, every comment
    /// lands Pending for manual moderation.</summary>
    public bool AutoApprove { get; set; } = true;
}
