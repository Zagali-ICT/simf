using SIMF.Common.Enums;

namespace SIMF.Domain.Programme;

/// <summary>
/// One attendee's arrival at a hall for a session: an enter time and, once they
/// leave or the session ends, a leave time. Recorded either by the attendee's own
/// device crossing a geofence or by an operator scanning at the hall door.
///
/// <para>There is at most one open row — one with no leave time — per attendee
/// per session, enforced by a filtered unique index, so a door scan and a
/// geofence crossing for the same session update the one row instead of creating
/// two.</para>
///
/// <para>GPS is sensitive personal data, so only the derived enter and leave
/// times are kept here, never the raw coordinates. The continuous track lives in
/// <see cref="DevicePositionPing"/> instead.</para>
/// </summary>
public sealed class HallAttendance
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>Denormalised from the session at record time, so the live
    /// per-hall counts need no join.</summary>
    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

    /// <summary>A bare Guid: the attendee lives in the Identity database, so
    /// there is no foreign key across the two.</summary>
    public Guid UserId { get; set; }

    public AttendanceMethod Method { get; set; }

    /// <summary>Saudi local time.</summary>
    public DateTime Enter { get; set; }

    /// <summary>Set on departure or at session end. Null while the attendee is
    /// still considered present, which is what makes the row open.</summary>
    public DateTime? Leave { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
