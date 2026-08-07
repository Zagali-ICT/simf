using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// A window of time during which a meeting <see cref="Hall"/> is available to
/// host business meetings. The admin review flow chops each window into
/// fixed-length slots and binds an accepted request to a free one. Symmetric with
/// <see cref="SpeakerAvailabilityWindow"/>.
/// </summary>
public sealed class HallAvailabilityWindow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Cascade-deleted with the hall.</summary>
    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

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
