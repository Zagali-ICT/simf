namespace SIMF.Contracts.Admin;

/// <summary>One row in the Scientific-Committee session-summary / محضر desk.
/// The desk lists every active session and its summary state, so a session with
/// no summary yet shows <see cref="HasSummary"/> = false and a Generate / Create
/// action. <see cref="GeneratedByAi"/> reflects whether the current draft was
/// AI-drafted (vs. hand-written); <see cref="IsPublished"/> whether the app can
/// read it.</summary>
public sealed record AdminSessionSummaryRow(
    Guid SessionId,
    string SessionCode,
    string SessionTitle,
    string SessionTitleArabic,
    DateTime SessionStart,
    bool HasSummary,
    bool GeneratedByAi,
    bool IsPublished,
    DateTime? PublishedAt,
    DateTime? UpdatedAt,
    // The team review/approval state, derived from the timestamps:
    // InReview = submitted but not yet approved; Approved = ready for المحاور.
    bool IsInReview,
    bool IsApproved,
    DateTime? ApprovedAt);

/// <summary>The full summary detail for the editor. Bilingual
/// content sections + the session header (read-only context) + provenance and
/// publish state. <see cref="AiModel"/> is non-null when the draft was AI-
/// generated. The two read-only AI-transparency sources
/// (<see cref="Subtitle"/> and <see cref="AiDraftFullTextArabic"/>) are shown
/// beside the editable fields — CP-internal, never on a public contract.</summary>
public sealed record AdminSessionSummaryDetail(
    Guid SessionId,
    string SessionCode,
    string SessionTitle,
    string SessionTitleArabic,
    string KeyPoints,
    string KeyPointsArabic,
    string Recommendations,
    string RecommendationsArabic,
    string Speakers,
    string SpeakersArabic,
    string FullText,
    string FullTextArabic,
    string? AiModel,
    bool IsPublished,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // Team review/approval state (derived from the timestamps).
    bool IsInReview,
    bool IsApproved,
    DateTime? ApprovedAt,
    // 2026-07-19 — AI-transparency read-only sources shown in the
    // editor: Subtitle / SubtitleArabic = the raw session captions the AI
    // drafted from (Session.LiveCaptions*); AiDraftFullTextArabic = the pristine
    // AI output captured at generation (immutable across edits) with
    // AiDraftGeneratedAt when it was captured. All read-only — the Committee's
    // edit round-trips only through SaveSessionSummaryRequest, which omits them.
    string? Subtitle,
    string? SubtitleArabic,
    string? AiDraftFullTextArabic,
    DateTime? AiDraftGeneratedAt,
    // The OPTIONAL team summary-video URL the editor
    // shows/saves (a YouTube or HLS/MP4 feed, LiveStreamUrlPolicy-validated).
    // Appended (defaulted) so the wire stays append-only. Null = no summary video.
    string? SummaryVideoUrl = null);

/// <summary>The Committee's edit (upsert) of a summary's content.
/// Saving a session that has no summary yet creates a hand-written draft
/// (<c>AiModel</c> stays null); the lengths align with the EF column limits.
/// Open class, matching the other admin-request contracts.</summary>
public class SaveSessionSummaryRequest
{
    public string KeyPoints { get; set; } = string.Empty;
    public string KeyPointsArabic { get; set; } = string.Empty;
    public string Recommendations { get; set; } = string.Empty;
    public string RecommendationsArabic { get; set; } = string.Empty;
    public string Speakers { get; set; } = string.Empty;
    public string SpeakersArabic { get; set; } = string.Empty;
    public string FullText { get; set; } = string.Empty;
    public string FullTextArabic { get; set; } = string.Empty;

    /// <summary>The OPTIONAL team summary-video URL, shown in the desk's second
    /// player. A YouTube watch/live URL or a direct HLS/MP4
    /// stream, validated server-side by <c>LiveStreamUrlPolicy</c> (the same rule
    /// as the session's live feed); null / blank = clear it. Max length aligns
    /// with the <c>SummaryVideoUrl</c> column (1024).</summary>
    public string? SummaryVideoUrl { get; set; }
}
