import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../../core/utils/saudi_time.dart';
import 'session_models.dart';
import 'sessions_endpoints.dart';

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
      SessionsEndpoints.attendance(sessionId),
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
      SessionsEndpoints.arrival(sessionId),
      body: <String, dynamic>{'lat': lat, 'lon': lon},
      decodeData: _decodeStatus,
    );
  }

  /// `POST /app/sessions/{id}/departure` → close the open attendance row.
  Future<HallAttendanceStatus> recordDeparture(String sessionId) {
    return _client.post<HallAttendanceStatus>(
      SessionsEndpoints.departure(sessionId),
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
