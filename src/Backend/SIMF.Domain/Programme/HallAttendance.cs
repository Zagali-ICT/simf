using SIMF.Common.Enums;

namespace SIMF.Domain.Programme;

/// <summary>
/// One attendee's arrival at a hall for a session, recorded by a geofence crossing
/// or an operator's door scan. Only the derived enter and leave times are kept,
/// never the raw coordinates: the track lives in <see cref="DevicePositionPing"/>.
/// </summary>
public sealed class HallAttendance
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>Denormalised from the session, so live per-hall counts need no join.</summary>
    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

    /// <summary>Keyed by profile, not account: a walk-in carries no account.</summary>
    public Guid UserProfileId { get; set; }
    public Profiles.UserProfile? UserProfile { get; set; }

    public AttendanceMethod Method { get; set; }

    /// <summary>Saudi local time.</summary>
    public DateTime Enter { get; set; }

    /// <summary>Null while the attendee is present, which is what makes a row open.</summary>
    public DateTime? Leave { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
