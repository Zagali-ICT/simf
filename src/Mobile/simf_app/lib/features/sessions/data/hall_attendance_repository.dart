import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../../core/utils/saudi_time.dart';

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

/// P5.1 / D-241 — the caller's own attendance state for one session.
/// [arrived] is true while an open attendance row exists (entered, not yet
/// left). [enter] / [leave] are stored UTC instants — render them through
/// [formatSaudiTime12], never `toLocal()` (D-770).
class HallAttendanceStatus {
  const HallAttendanceStatus({
    required this.arrived,
    this.enter,
    this.leave,
    this.method,
  });

  final bool arrived;
  final DateTime? enter;
  final DateTime? leave;
  final HallAttendanceMethod? method;

  static HallAttendanceStatus fromJson(Map<String, dynamic> json) {
    return HallAttendanceStatus(
      arrived: json['arrived'] as bool? ?? false,
      enter: json['enter'] == null ? null : parseWireUtc(json['enter'], 'enter'),
      leave: json['leave'] == null ? null : parseWireUtc(json['leave'], 'leave'),
      method: HallAttendanceMethod.fromJson(json['method']),
    );
  }
}

/// Data layer for the attendee's own hall arrival / departure (FR-305/506,
/// D-241). The device reports its position; the SERVER decides — it checks the
/// point against the session hall's geofence and records the arrival. The raw
/// coordinates are never persisted.
///
/// Throws [ApiFailure] on a wire error. The codes the check-in action branches
/// on are published as [hallGeofenceNotConfigured] (the hall has no boundary
/// yet — the feature is inert until the CP sets one), [notAtVenue] (the caller
/// is outside the radius) and [sessionNotLive] (outside the arrival window).
class HallAttendanceRepository {
  HallAttendanceRepository(this._client);

  /// `HALL_GEOFENCE_NOT_CONFIGURED` — the hall has no lat/lon/radius, so
  /// arrival is recorded by a door scan instead. Expected until a hall is
  /// given a boundary in the Control Panel; not an app defect.
  static const String hallGeofenceNotConfigured =
      'HALL_GEOFENCE_NOT_CONFIGURED';

  /// `NOT_AT_VENUE` — the reported position is outside the hall radius.
  static const String notAtVenue = 'NOT_AT_VENUE';

  /// `SESSION_NOT_LIVE` — outside the session window (± the server's grace).
  static const String sessionNotLive = 'SESSION_NOT_LIVE';

  final SimfApiClient _client;

  /// `GET /app/sessions/{id}/attendance` → the caller's current state.
  Future<HallAttendanceStatus> getStatus(String sessionId) {
    return _client.get<HallAttendanceStatus>(
      '/app/sessions/$sessionId/attendance',
      decodeData: _decodeStatus,
    );
  }

  /// `POST /app/sessions/{id}/arrival` → claim arrival from the device fix.
  Future<HallAttendanceStatus> recordArrival(
    String sessionId, {
    required double lat,
    required double lon,
  }) {
    return _client.post<HallAttendanceStatus>(
      '/app/sessions/$sessionId/arrival',
      body: <String, dynamic>{'lat': lat, 'lon': lon},
      decodeData: _decodeStatus,
    );
  }

  /// `POST /app/sessions/{id}/departure` → close the open attendance row.
  Future<HallAttendanceStatus> recordDeparture(String sessionId) {
    return _client.post<HallAttendanceStatus>(
      '/app/sessions/$sessionId/departure',
      // An empty JSON object, not a null body — a bodyless POST is rejected
      // with 400 VALIDATION_FAILED before the handler runs.
      body: const <String, dynamic>{},
      decodeData: _decodeStatus,
    );
  }

  static HallAttendanceStatus _decodeStatus(Object? data) =>
      HallAttendanceStatus.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      );
}

final hallAttendanceRepositoryProvider =
    Provider<HallAttendanceRepository>((ref) {
  return HallAttendanceRepository(ref.watch(simfApiClientProvider));
});

/// The caller's attendance state for [sessionId] as one cached async read.
/// `autoDispose.family` so a fresh mount or an `ref.invalidate` re-fetches.
final hallAttendanceStatusProvider =
    FutureProvider.autoDispose.family<HallAttendanceStatus, String>(
  (ref, sessionId) =>
      ref.watch(hallAttendanceRepositoryProvider).getStatus(sessionId),
);
