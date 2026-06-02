using SIMF.Common.Enums;

namespace SIMF.Domain.Programme;

/// <summary>
/// P5.1 — D-241 (FDS-003 §5.4, FR-305/506): one attendee's arrival at a hall for
/// a session — an enter time and (once they leave or the session ends) a leave
/// time. Recorded by a GPS geofence crossing (<see cref="AttendanceMethod.Geofence"/>,
/// the attendee's own device) or a QR scan at the hall door
/// (<see cref="AttendanceMethod.QrScan"/>, an operator). There is at most one
/// <b>open</b> row (LeaveUtc null) per attendee per session — a door scan and a
/// geofence crossing for the same session update the one row rather than
/// creating two (enforced by a filtered unique index).
///
/// <para>Feeds session attendance (FR-506) and gates audience questions
/// (FR-704). GPS is <b>sensitive personal data</b> (FDS-003 §10): only the
/// derived enter/leave times are stored here — never the raw coordinates or a
/// continuous track (that is the deferred movement/dwell feature, FR-1103).</para>
/// </summary>
public sealed class HallAttendance
{
    public Guid Id { get; set; }

    /// <summary>The session attended. Real FK (same DbContext).</summary>
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>The hall the session is in (denormalised from the session at
    /// record time for the live-attendance per-hall counts). Real FK.</summary>
    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

    /// <summary>Bare <c>Guid</c> of the attendee — cross-context (Identity DB),
    /// no FK (D-157).</summary>
    public Guid UserId { get; set; }

    /// <summary>How this arrival was recorded.</summary>
    public AttendanceMethod Method { get; set; }

    /// <summary>When the attendee entered (UTC).</summary>
    public DateTimeOffset EnterUtc { get; set; }

    /// <summary>When the attendee left — set on departure or at session end.
    /// Null while the attendee is still considered present (the open row).</summary>
    public DateTimeOffset? LeaveUtc { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
