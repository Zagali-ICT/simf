using SIMF.Common.Enums;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// D-717 (item 7, FDS-013 §15.7 GAP-3) — a single-use, short-lived, action-bound
/// token behind a speaker double-opt-in email link. Two are minted per
/// accept-with-hall request (one <see cref="MeetingActionType.Approve"/>, one
/// <see cref="MeetingActionType.Reject"/>); the speaker clicking a link is the
/// final gate. Only the keyed-HMAC <see cref="TokenHash"/> of the high-entropy
/// secret is persisted — the raw secret lives only in the emailed URL. A token is
/// dead once <see cref="UsedAt"/> is set, once <see cref="Expires"/> passes, or
/// once its request leaves <c>AwaitingSpeaker</c> (validated on redemption).
/// </summary>
public sealed class MeetingActionToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The speaker meeting request this token decides. Real FK to
    /// <see cref="SpeakerMeetingRequest"/> on the App DB (cascade — a deleted
    /// request removes its tokens).</summary>
    public Guid SpeakerMeetingRequestId { get; set; }
    public SpeakerMeetingRequest? SpeakerMeetingRequest { get; set; }

    /// <summary>Which decision this token authorises (baked in, §15.7).</summary>
    public MeetingActionType Action { get; set; }

    /// <summary>Keyed-HMAC (SHA-256) hash of the high-entropy secret — the value
    /// compared on redemption. The raw secret is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>When the token expires (UTC) — 72h after mint (§15.7 / OI-I).</summary>
    public DateTimeOffset Expires { get; set; }

    /// <summary>When the token was consumed (UTC); null while unused. Single-use.</summary>
    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
