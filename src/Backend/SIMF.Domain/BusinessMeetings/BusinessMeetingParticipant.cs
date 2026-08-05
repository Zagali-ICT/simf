using SIMF.Common.Enums;
using SIMF.Domain.Exhibitors;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// One party in a <see cref="BusinessMeeting"/>. A company participant carries a
/// real foreign key to its exhibitor, which lives in this database; a visitor
/// participant carries only a bare Guid, because the user lives in the Identity
/// database and a foreign key cannot cross the two.
/// </summary>
public sealed class BusinessMeetingParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Cascade-deleted with the meeting.</summary>
    public Guid BusinessMeetingId { get; set; }
    public BusinessMeeting? BusinessMeeting { get; set; }

    public MeetingPartyKind Kind { get; set; }

    /// <summary>Set when <see cref="Kind"/> is Company, null otherwise.</summary>
    public Guid? ExhibitorId { get; set; }
    public Exhibitor? Exhibitor { get; set; }

    /// <summary>Set when <see cref="Kind"/> is Visitor, null otherwise. No
    /// navigation and no database foreign key.</summary>
    public Guid? VisitorUserId { get; set; }

    /// <summary>The party's display name as it stood when the meeting was
    /// scheduled. One of the few permitted copies of Identity-owned data, so the
    /// meeting record stays readable on its own.</summary>
    public string DisplayNameSnapshot { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
