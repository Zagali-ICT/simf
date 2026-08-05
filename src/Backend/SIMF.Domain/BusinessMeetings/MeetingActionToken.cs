using SIMF.Common.Enums;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// A single-use, short-lived token bound to one decision, sitting behind a
/// speaker's double-opt-in email link. Two are minted per accept-with-hall
/// request, one to approve and one to reject, and the speaker's click is the
/// final gate.
///
/// <para>A token is dead once it has been used, once it expires, or once its
/// request leaves the awaiting-speaker state — all three checked on
/// redemption.</para>
/// </summary>
public sealed class MeetingActionToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Cascade-deleted with the request it decides.</summary>
    public Guid SpeakerMeetingRequestId { get; set; }
    public SpeakerMeetingRequest? SpeakerMeetingRequest { get; set; }

    /// <summary>Which decision this token authorises, baked in at mint
    /// time.</summary>
    public MeetingActionType Action { get; set; }

    /// <summary>A keyed HMAC of the high-entropy secret, and the value compared
    /// on redemption. The secret itself is never stored; it exists only in the
    /// emailed URL.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Saudi local time, at the configured lifetime after mint.</summary>
    public DateTime Expires { get; set; }

    /// <summary>When the token was consumed; null while unused.</summary>
    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
