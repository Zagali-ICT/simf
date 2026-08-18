import 'package:simf_app/core/utils/saudi_time.dart';

/// P5.1 / D-241 — the caller's own attendance state for one session.
/// [arrived] is true while an open attendance row exists (entered, not yet
/// left). [enter] / [leave] are stored absolute instants — render them through
/// [formatSaudiTime12], never `toLocal()` (D-770).
class HallAttendanceStatus {
  const HallAttendanceStatus({
    required this.arrived,
    this.enter,
    this.leave,
    this.method,
  });

  factory HallAttendanceStatus.fromJson(Map<String, dynamic> json) {
    return HallAttendanceStatus(
      arrived: json['arrived'] as bool? ?? false,
      enter: json['enter'] == null
          ? null
          : parseWireDateTime(json['enter'], 'enter'),
      leave: json['leave'] == null
          ? null
          : parseWireDateTime(json['leave'], 'leave'),
      method: HallAttendanceMethod.fromJson(json['method']),
    );
  }

  final bool arrived;
  final DateTime? enter;
  final DateTime? leave;
  final HallAttendanceMethod? method;
}

/// How the attendee's presence in the hall was recorded — an operator scanning
/// the badge QR at the door, or the attendee's own device crossing the GPS
/// geofence. Int on the wire (mirrors `SIMF.Common.Enums.AttendanceMethod`);
/// [fromJson] is tolerant (int OR name) like the app's other server enums, and
/// a null / absent / unknown value decodes to null rather than throwing.
enum HallAttendanceMethod {
  qrScan(0, 'QrScan'),
  geofence(1, 'Geofence');

  const HallAttendanceMethod(this.wireValue, this.wireName);

  final int wireValue;
  final String wireName;

  static HallAttendanceMethod? fromJson(Object? value) {
    if (value is String) {
      for (final m in values) {
        if (m.wireName == value) {
          return m;
        }
      }
    } else if (value is num) {
      final asInt = value.toInt();
      for (final m in values) {
        if (m.wireValue == asInt) {
          return m;
        }
      }
    }
    return null;
  }
}
