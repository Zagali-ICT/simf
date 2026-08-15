using SIMF.Common.Enums;
using SIMF.Domain.Exhibitors;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>One party in a <see cref="BusinessMeeting"/>: an exhibitor, or a visitor, whose
/// user lives in the Identity database and so is a bare Guid with no foreign key.</summary>
public sealed class BusinessMeetingParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BusinessMeetingId { get; set; }
    public BusinessMeeting? BusinessMeeting { get; set; }

    public MeetingPartyKind Kind { get; set; }

    public Guid? ExhibitorId { get; set; }
    public Exhibitor? Exhibitor { get; set; }

    public Guid? VisitorUserId { get; set; }

    /// <summary>A permitted audit snapshot of Identity-owned data, taken at scheduling time
    /// so the meeting record stays readable on its own.</summary>
    public string DisplayNameSnapshot { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
