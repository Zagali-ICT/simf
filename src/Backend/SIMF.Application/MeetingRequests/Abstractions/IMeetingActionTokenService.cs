using SIMF.Contracts.Programme;

namespace SIMF.Application.MeetingRequests.Abstractions;

/// <summary>D-717 (item 7, FDS-013 §15 GAP-3) — the speaker double-opt-in
/// action-link tokens. Minting is called from the accept-with-hall flow;
/// <see cref="PreviewAsync"/> + <see cref="ApplyAsync"/> back the two public
/// <c>AllowAnonymous</c> endpoints.</summary>
public interface IMeetingActionTokenService
{
    /// <summary>Stage the Approve + Reject tokens for a request into the shared
    /// DbContext WITHOUT saving, and return the two ready-built landing-page URLs.
    /// The caller commits them in the SAME <c>SaveChanges</c> as the
    /// <c>AwaitingSpeaker</c> transition, so a request can never be AwaitingSpeaker
    /// without its token pair (D-717). The raw secret lives only in the emailed URL —
    /// only its hash is persisted. URLs are empty when the public base URL is
    /// unconfigured (the caller then skips the email; the tokens still commit).</summary>
    MeetingActionLinks StageTokensForRequest(Guid speakerMeetingRequestId);

    /// <summary>QA A24 — is <c>MeetingLinks:PublicWebBaseUrl</c> configured, i.e. can a
    /// landing-page URL actually be built? The approve / resend paths check this BEFORE
    /// they mint anything, so a missing setting is a clean up-front failure instead of a
    /// request parked in <c>AwaitingSpeaker</c> whose only exit is an email that was never
    /// sent. Keeps the option key knowledge in one place (this service builds the URLs).</summary>
    bool LinksConfigured { get; }

    /// <summary>Write the "minted" OperationLog row (§15.7). Called AFTER the caller
    /// commits the staged tokens, so the audit only records a durable mint.</summary>
    Task AuditMintedAsync(
        Guid speakerMeetingRequestId, CancellationToken cancellationToken = default);

    /// <summary>Stage ONE single-use delegation confirm token for a request
    /// into the shared DbContext WITHOUT saving, and return its ready-built landing-page
    /// URL (the same public <c>/meeting/confirm</c> page the speaker links use). The
    /// caller commits it in the SAME <c>SaveChanges</c> as the <c>AwaitingSpeaker</c>
    /// transition; the same URL is emailed to every eligible target member and the FIRST
    /// click confirms (mirroring the in-app tap). Confirm-only — no decline link. Empty
    /// when the public base URL is unconfigured (the caller then skips the email; the
    /// token still commits). <see cref="PreviewAsync"/> / <see cref="ApplyAsync"/> then
    /// serve BOTH the speaker and the delegation token behind the one endpoint.</summary>
    string StageDelegationConfirmToken(Guid delegationMeetingRequestId);

    /// <summary>Validate a token WITHOUT consuming it and return the meeting
    /// preview, or <c>null</c> if it is unusable (not found / expired / used / the
    /// request is no longer awaiting the speaker). GET-safe — a link prefetcher
    /// cannot consume the token (§15.7).</summary>
    Task<MeetingActionPreview?> PreviewAsync(
        string tokenSecret, CancellationToken cancellationToken = default);

    /// <summary>Consume a token and apply its decision (Approve → Accepted + notify
    /// the requester "confirmed"; Reject → Rejected + notify "cancelled"). Returns
    /// <c>null</c> if the token is unusable, so a replay is a neutral no-op.</summary>
    Task<MeetingActionOutcome?> ApplyAsync(
        string tokenSecret, CancellationToken cancellationToken = default);
}

/// <summary>The two email-link URLs a mint produced. Empty when the public
/// base URL is unconfigured.</summary>
public sealed record MeetingActionLinks(string ApproveUrl, string RejectUrl)
{
    public bool HasUrls =>
        !string.IsNullOrEmpty(ApproveUrl) && !string.IsNullOrEmpty(RejectUrl);
}
