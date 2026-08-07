using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// A window of time during which a <see cref="Speaker"/> is available to meet.
/// The meeting flow chops each window into fixed-length slots and offers the free
/// ones to the requester; the team then confirms the chosen slot and the speaker
/// is emailed.
/// </summary>
public sealed class SpeakerAvailabilityWindow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Cascade-deleted with the speaker.</summary>
    public Guid SpeakerId { get; set; }
    public Speaker? Speaker { get; set; }

    /// <summary>Saudi local time.</summary>
    public DateTime Start { get; set; }

    /// <summary>Saudi local time, and must be after <see cref="Start"/>.</summary>
    public DateTime End { get; set; }

    /// <summary>The window divides into back-to-back slots of this length. A
    /// trailing remainder too short for one slot is ignored.</summary>
    public int SlotMinutes { get; set; } = 30;

    /// <summary>Soft-delete flag, hiding the window without losing the row.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>A bare Guid: the admin lives in the Identity database.</summary>
    public Guid? CreatedByUserId { get; set; }
}
