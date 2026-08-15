using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>A window of time during which a <see cref="Speaker"/> is available to meet, divided into fixed-length bookable slots.</summary>
public sealed class SpeakerAvailabilityWindow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SpeakerId { get; set; }
    public Speaker? Speaker { get; set; }

    /// <summary>Saudi local time.</summary>
    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public int SlotMinutes { get; set; } = 30;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>A bare Guid: the admin lives in the Identity database.</summary>
    public Guid? CreatedByUserId { get; set; }
}
