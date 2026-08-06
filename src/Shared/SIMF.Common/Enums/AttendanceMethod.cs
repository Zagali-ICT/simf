namespace SIMF.Common.Enums;

/// <summary>How a hall arrival was recorded.
/// A QR scan at the hall door (operator) and a GPS geofence crossing (the
/// attendee's own device) are the two means; both write into the one open
/// <c>HallAttendance</c> row per attendee per session.</summary>
public enum AttendanceMethod
{
    QrScan = 0,
    Geofence = 1,
}
