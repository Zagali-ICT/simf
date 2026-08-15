using SIMF.Common.Enums;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>One admin-arranged business meeting at a meeting table. Arranged from the
/// Control Panel only, with no request queue behind it, so a meeting is created already
/// confirmed and the only other state is cancelled.</summary>
public sealed class BusinessMeeting
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MeetingTableId { get; set; }
    public MeetingTable? MeetingTable { get; set; }

    /// <summary>A label the admin sets, not derived from the mix of participants.</summary>
    public BusinessMeetingType MeetingType { get; set; }

    /// <summary>Inclusive; <see cref="End"/> is exclusive. Both Saudi local time.</summary>
    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public BusinessMeetingStatus Status { get; set; } = BusinessMeetingStatus.Confirmed;

    public string? Notes { get; set; }

    /// <summary>Identity-database user: a bare Guid, never a foreign key.</summary>
    public Guid ScheduledByUserId { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>At least two, and more for a group meeting.</summary>
    public List<BusinessMeetingParticipant> Participants { get; set; } = [];
}
