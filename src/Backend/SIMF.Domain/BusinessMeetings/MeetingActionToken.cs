using SIMF.Common.Enums;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>The single-use token behind a speaker's double-opt-in email link: two per
/// accept-with-hall request, one to approve and one to reject. Redemption rejects a token
/// that is used, expired, or whose request has left the awaiting-speaker state.</summary>
public sealed class MeetingActionToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SpeakerMeetingRequestId { get; set; }
    public SpeakerMeetingRequest? SpeakerMeetingRequest { get; set; }

    public MeetingActionType Action { get; set; }

    /// <summary>A keyed HMAC; the secret is never stored, and exists only in the emailed URL.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Saudi local time, at the configured lifetime after mint.</summary>
    public DateTime Expires { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
