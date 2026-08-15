namespace SIMF.Domain.BusinessMeetings;

/// <summary>The single-use token behind the delegation "please confirm" email link. The same
/// link goes to every eligible member of the target delegation, so the first click confirms
/// for them all and the rest race harmlessly to the same guarded flip. Confirm only; declines
/// stay with the admin. Rejected on redemption if used, expired, or no longer awaiting.</summary>
public sealed class DelegationMeetingActionToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DelegationMeetingRequestId { get; set; }
    public DelegationMeetingRequest? DelegationMeetingRequest { get; set; }

    /// <summary>A keyed HMAC; the secret is never stored, and exists only in the emailed URL.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Saudi local time, on the same lifetime the speaker token uses.</summary>
    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
