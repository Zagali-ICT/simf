using SIMF.Common.Enums;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// SIMF-FDS-013 (owner, 2026-06-03) — one admin-arranged B2B/B2C business meeting
/// scheduled at a <see cref="MeetingTable"/> for a from–to time-slot. Meetings are
/// arranged from the Control Panel only (no attendee request queue), so they are
/// created <see cref="BusinessMeetingStatus.Confirmed"/> and can be Cancelled. The
/// participants (two or more — group meetings allowed) are <see cref="Participants"/>.
/// </summary>
public sealed class BusinessMeeting
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Real FK to <see cref="MeetingTable.Id"/> on the App DB.</summary>
    public Guid MeetingTableId { get; set; }
    public MeetingTable? MeetingTable { get; set; }

    /// <summary>The category — set explicitly by the admin (OI-10); participants
    /// may be any mix of companies and visitors.</summary>
    public BusinessMeetingType MeetingType { get; set; }

    /// <summary>Slot start (inclusive, UTC).</summary>
    public DateTimeOffset StartUtc { get; set; }

    /// <summary>Slot end (exclusive, UTC). Must be after <see cref="StartUtc"/>.</summary>
    public DateTimeOffset EndUtc { get; set; }

    /// <summary>Lifecycle state. Confirmed on create; Cancelled after an admin
    /// cancels.</summary>
    public BusinessMeetingStatus Status { get; set; } = BusinessMeetingStatus.Confirmed;

    /// <summary>Optional admin note / subject (≤ 1024 chars).</summary>
    public string? Notes { get; set; }

    /// <summary>The admin who scheduled the meeting — logical FK to
    /// <c>SimfUser.Id</c> on the Identity DB (bare Guid).</summary>
    public Guid ScheduledByUserId { get; set; }

    /// <summary>The admin who cancelled it — logical FK to the Identity DB; null
    /// while Confirmed.</summary>
    public Guid? CancelledByUserId { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>Optional reason recorded on cancel (≤ 512 chars, OI-6).</summary>
    public string? CancellationReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>The meeting's participants (≥ 2). Cascade-deleted with the meeting.</summary>
    public List<BusinessMeetingParticipant> Participants { get; set; } = [];
}
