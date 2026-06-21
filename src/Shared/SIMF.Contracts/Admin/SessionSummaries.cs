namespace SIMF.Contracts.Admin;

/// <summary>P4.1 — D-238 (Completion Programme §6.4.1, Mockup screen 34): one
/// row in the Scientific-Committee session-summary / محضر desk. The desk lists
/// every active session and its summary state, so a session with no summary
/// yet shows <see cref="HasSummary"/> = false and a Generate / Create action.
/// <see cref="GeneratedByAi"/> reflects whether the current draft was AI-drafted
/// (vs. hand-written); <see cref="IsPublished"/> whether the app can read it.</summary>
public sealed record AdminSessionSummaryRow(
    Guid SessionId,
    string SessionCode,
    string SessionTitle,
    string SessionTitleArabic,
    DateTimeOffset SessionStartUtc,
    bool HasSummary,
    bool GeneratedByAi,
    bool IsPublished,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? UpdatedAt,
    // D-472 (#9) — the team review/approval state, derived from the timestamps:
    // InReview = submitted but not yet approved; Approved = ready for المحاور.
    bool IsInReview,
    bool IsApproved,
    DateTimeOffset? ApprovedAt);

/// <summary>P4.1 — D-238: the full summary detail for the editor. Bilingual
/// content sections + the session header (read-only context) + provenance and
/// publish state. <see cref="AiModel"/> is non-null when the draft was AI-
/// generated.</summary>
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
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    // D-472 (#9) — team review/approval state (derived from the timestamps).
    bool IsInReview,
    bool IsApproved,
    DateTimeOffset? ApprovedAt);

/// <summary>P4.1 — D-238: the Committee's edit (upsert) of a summary's content.
/// Saving a session that has no summary yet creates a hand-written draft
/// (<c>AiModel</c> stays null); the lengths align with the EF column limits
/// (§7). Open class per the D-168 / D-174 admin-request pattern.</summary>
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
}
