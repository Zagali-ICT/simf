using SIMF.Common.Enums;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// One admin-arranged business meeting, scheduled at a <see cref="MeetingTable"/>
/// for a time slot. Meetings are arranged from the Control Panel only, with no
/// attendee request queue behind them, so a meeting is created already confirmed
/// and the only other state is cancelled.
/// </summary>
public sealed class BusinessMeeting
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MeetingTableId { get; set; }
    public MeetingTable? MeetingTable { get; set; }

    /// <summary>Set explicitly by the admin. Participants may be any mix of
    /// companies and visitors, so this is a label rather than something derived
    /// from them.</summary>
    public BusinessMeetingType MeetingType { get; set; }

    /// <summary>Inclusive, Saudi local time.</summary>
    public DateTime Start { get; set; }

    /// <summary>Exclusive, Saudi local time, and must be after
    /// <see cref="Start"/>.</summary>
    public DateTime End { get; set; }

    public BusinessMeetingStatus Status { get; set; } = BusinessMeetingStatus.Confirmed;

    /// <summary>An admin note or subject line.</summary>
    public string? Notes { get; set; }

    /// <summary>The admin who scheduled it. A bare Guid: the user lives in the
    /// Identity database.</summary>
    public Guid ScheduledByUserId { get; set; }

    /// <summary>The admin who cancelled it; null while the meeting stands.</summary>
    public Guid? CancelledByUserId { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>At least two, and more for a group meeting. Cascade-deleted with
    /// the meeting.</summary>
    public List<BusinessMeetingParticipant> Participants { get; set; } = [];
}
