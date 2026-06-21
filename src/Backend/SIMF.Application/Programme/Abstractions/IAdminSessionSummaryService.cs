using SIMF.Contracts.Admin;

namespace SIMF.Application.Programme.Abstractions;

/// <summary>P4.1 — D-238 (Completion Programme §6.4.1, Mockup screen 34): the
/// Scientific-Committee session-summary / محضر desk. The Committee drafts a
/// summary — AI-assisted via <see cref="GenerateAsync"/> (routed through the
/// central AI provider seam) or hand-written via <see cref="SaveAsync"/> —
/// reviews and edits it, then publishes it for the app to read
/// (<c>GET /programme/sessions/{id}/summary</c>, D-237). There is one summary
/// per session; publishing flips its visibility and subsequent edits are live
/// (the Committee can un-publish to take it offline while editing).</summary>
public interface IAdminSessionSummaryService
{
    /// <summary>Every active session with its summary state — newest session
    /// first. Sessions with no summary appear with <c>HasSummary = false</c>.</summary>
    Task<IReadOnlyList<AdminSessionSummaryRow>> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>The summary detail for one session's editor, or null when the
    /// session is missing / soft-deleted or has no summary yet.</summary>
    Task<AdminSessionSummaryDetail?> GetAsync(
        Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>AI-draft the summary from the session metadata (through the
    /// <c>session-summary</c> prompt, which produces ARABIC minutes). Upserts the
    /// summary, writes the draft into the <b>Arabic</b> full-text column only, and
    /// stamps the AI model; the English column and the curated sections are left
    /// untouched so a re-generate preserves the Committee's edits. Never
    /// publishes. 404 when the session is missing.</summary>
    Task<AdminSessionSummaryDetail> GenerateAsync(
        Guid actorUserId, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Upsert the Committee's edited content (creates a hand-written
    /// draft when none exists). Validates the section lengths. 404 when the
    /// session is missing / soft-deleted.</summary>
    Task<AdminSessionSummaryDetail> SaveAsync(
        Guid actorUserId, Guid sessionId, SaveSessionSummaryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Publish the summary (stamp <c>PublishedAt</c>) so the app can
    /// read it. 404 when no summary exists yet.</summary>
    Task<AdminSessionSummaryDetail> PublishAsync(
        Guid actorUserId, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Take a published summary offline (clear <c>PublishedAt</c>).
    /// 404 when no summary exists.</summary>
    Task<AdminSessionSummaryDetail> UnpublishAsync(
        Guid actorUserId, Guid sessionId, CancellationToken cancellationToken = default);

    // -- D-472 (requirement #9) — the team review/approval workflow -----------
    // Draft → (SubmitForReview) → InReview → (Approve) → Approved = ready for
    // المحاور. Any content edit (SaveAsync / GenerateAsync) returns it to Draft.

    /// <summary>Submit the draft for the team's approval (Draft → InReview).
    /// 404 when no summary exists; 400 when it is already approved (return it to
    /// draft first).</summary>
    Task<AdminSessionSummaryDetail> SubmitForReviewAsync(
        Guid actorUserId, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Approve a submitted summary (InReview → Approved); the approved
    /// محضر then becomes readable by the session's host / moderator. 404 when no
    /// summary exists; 400 when it has not been submitted for review.</summary>
    Task<AdminSessionSummaryDetail> ApproveAsync(
        Guid actorUserId, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Send an in-review / approved summary back to Draft (clears both
    /// the review and approval stamps). 404 when no summary exists.</summary>
    Task<AdminSessionSummaryDetail> ReturnToDraftAsync(
        Guid actorUserId, Guid sessionId, CancellationToken cancellationToken = default);
}
