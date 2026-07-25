using SIMF.Common.Enums;

namespace SIMF.Contracts.Programme;

/// <summary>D-717 (item 7, FDS-013 §15 GAP-3) — what the public token landing page
/// shows before the speaker confirms (from <c>GET /app/meeting-actions/{token}</c>).
/// Returned only for a still-valid token; the page needs enough to let the speaker
/// decide (who wants to meet, about what, when, where). No requester email / no
/// account ids — the opaque token in the URL is the only credential, and the URL
/// itself carries no PII (§15.7).</summary>
public sealed record MeetingActionPreview(
    MeetingActionType Action,
    string SpeakerName,
    string SpeakerNameArabic,
    string RequesterName,
    string Subject,
    DateTimeOffset? SlotStart,
    DateTimeOffset? SlotEnd,
    string? HallName);

/// <summary>D-717 — the result of confirming a token
/// (<c>POST /app/meeting-actions/{token}</c>): the decision that was applied, so
/// the landing page can render "you approved / rejected this meeting".</summary>
public sealed record MeetingActionOutcome(
    MeetingActionType Action);
