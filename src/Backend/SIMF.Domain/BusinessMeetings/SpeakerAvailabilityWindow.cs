using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// D-474 (#11, Group G phase 1) — a window of time during which a
/// <see cref="Speaker"/> is available to meet, defined by the team. The
/// VIP-meeting flow chops each window into fixed-length slots
/// (<see cref="SlotMinutes"/>) and offers the free ones to the requester; the
/// team then confirms the picked slot and the speaker is emailed.
/// </summary>
public sealed class SpeakerAvailabilityWindow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The speaker this window belongs to. Real FK to
    /// <see cref="Speaker"/> on the App DB (cascade — a deleted speaker removes
    /// its windows).</summary>
    public Guid SpeakerId { get; set; }
    public Speaker? Speaker { get; set; }

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
