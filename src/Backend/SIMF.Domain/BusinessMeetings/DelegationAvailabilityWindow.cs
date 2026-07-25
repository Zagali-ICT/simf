using SIMF.Domain.Common;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// Bi-Meeting rework — a window of time during which a delegation (identified by
/// its <see cref="Country"/>) is available to meet, defined by the team. Mirrors
/// <see cref="SpeakerAvailabilityWindow"/>: the delegation-meeting flow chops each
/// window into fixed-length slots (<see cref="SlotMinutes"/>) and offers the free
/// ones to a requester from another delegation, so a delegation meeting can be
/// booked against real available time (parity with the speaker flow).
/// </summary>
public sealed class DelegationAvailabilityWindow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The delegation (country) this window belongs to. Real FK to
    /// <see cref="Country"/> on the App DB (Restrict — a country in use cannot be
    /// removed), mirroring how other App entities reference the Country lookup.</summary>
    public int CountryId { get; set; }
    public Country? Country { get; set; }

    /// <summary>Window start (UTC).</summary>
    public DateTimeOffset Start { get; set; }

    /// <summary>Window end (UTC). Must be after <see cref="Start"/>.</summary>
    public DateTimeOffset End { get; set; }

    /// <summary>Slot length in minutes (e.g. 30). The window is divided into
    /// back-to-back slots of this length; a trailing remainder shorter than one
    /// slot is ignored.</summary>
    public int SlotMinutes { get; set; } = 30;

    /// <summary>Soft-delete flag — hides the window without losing the row.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Bare <c>Guid</c> of the admin who created it — cross-context, no FK (D-157).</summary>
    public Guid? CreatedByUserId { get; set; }
}
