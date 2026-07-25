using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// D-715 (item 7, FDS-013 §15 GAP-1) — a window of time during which a
/// <see cref="Hall"/> (purpose Meeting) is available to host business meetings,
/// defined by the team. The admin review flow chops each window into fixed-length
/// slots (<see cref="SlotMinutes"/>) and binds an accepted meeting request to a
/// free one. Symmetric with <see cref="SpeakerAvailabilityWindow"/>.
/// </summary>
public sealed class HallAvailabilityWindow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The hall this window belongs to. Real FK to <see cref="Hall"/>
    /// on the App DB (cascade — a deleted hall removes its windows).</summary>
    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

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
