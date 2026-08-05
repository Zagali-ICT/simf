namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// The single-use, short-lived token behind the delegation "please confirm"
/// email link. One is minted when an admin approves a
/// <see cref="DelegationMeetingRequest"/>, and the same link goes to every
/// eligible member of the target delegation, so the first click confirms on the
/// delegation's behalf and the rest race harmlessly to the same guarded flip.
///
/// <para>Confirm only, with no decline link: the in-app flow has no member
/// decline either, and declines stay with the admin.</para>
///
/// <para>A token is dead once it has been used, once it expires, or once its
/// request leaves the awaiting-confirmation state — all three checked on
/// redemption. It exists as its own table rather than as columns on the speaker
/// <see cref="MeetingActionToken"/>, which was frozen.</para>
/// </summary>
public sealed class DelegationMeetingActionToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Cascade-deleted with the request it confirms.</summary>
    public Guid DelegationMeetingRequestId { get; set; }
    public DelegationMeetingRequest? DelegationMeetingRequest { get; set; }

    /// <summary>A keyed HMAC of the high-entropy secret, and the value compared
    /// on redemption. The secret itself is never stored; it exists only in the
    /// emailed URL.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Saudi local time, and the same lifetime the speaker token
    /// uses.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>When the token was consumed; null while unused.</summary>
    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
