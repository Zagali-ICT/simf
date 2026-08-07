using SIMF.Domain.Common;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// A window of time during which a delegation, identified by its country, is
/// available to meet. The delegation-meeting flow chops each window into
/// fixed-length slots and offers the free ones to a requester from another
/// delegation, so a meeting is booked against real available time. The
/// counterpart of <see cref="SpeakerAvailabilityWindow"/>.
/// </summary>
public sealed class DelegationAvailabilityWindow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The delegation this window belongs to. A real foreign key with
    /// delete restricted, so a country in use cannot be removed.</summary>
    public int CountryId { get; set; }
    public Country? Country { get; set; }

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
