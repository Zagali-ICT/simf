namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// R4 (bi-meeting rules, D-767) — a single-use, short-lived confirm token behind the
/// delegation "please confirm" email link. Minted once when an admin Approves a
/// <see cref="DelegationMeetingRequest"/> (it moves to <c>AwaitingSpeaker</c>); the
/// same link is emailed to every eligible target-delegation member, and the FIRST
/// click confirms the meeting on the delegation's behalf — mirroring the in-app tap
/// (<c>ConfirmByOtherPartyAsync</c>), so member A's click and member B's click race to
/// the one race-safe flip. Confirm-only (no decline link): the in-app flow has no
/// member-decline either; declines stay with the admin. Only the keyed-HMAC
/// <see cref="TokenHash"/> of the high-entropy secret is persisted — the raw secret
/// lives only in the emailed URL. A token is dead once <see cref="UsedAt"/> is set,
/// once <see cref="ExpiresAt"/> passes, or once its request leaves
/// <c>AwaitingSpeaker</c> (validated on redemption). New additive table (D-219 lift);
/// the frozen speaker <see cref="MeetingActionToken"/> table is left untouched (§20 —
/// indirect solution over modifying frozen code).
/// </summary>
public sealed class DelegationMeetingActionToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The delegation meeting request this token confirms. Real FK to
    /// <see cref="DelegationMeetingRequest"/> on the App DB (cascade — a deleted
    /// request removes its tokens).</summary>
    public Guid DelegationMeetingRequestId { get; set; }
    public DelegationMeetingRequest? DelegationMeetingRequest { get; set; }

    /// <summary>Keyed-HMAC (SHA-256) hash of the high-entropy secret — the value
    /// compared on redemption. The raw secret is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>When the token expires (Saudi local) — 72h after mint (mirrors the speaker
    /// token TTL, <see cref="Common.Options.MeetingLinksOptions.TokenTtlHours"/>).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>When the token was consumed (Saudi local); null while unused. Single-use.</summary>
    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
