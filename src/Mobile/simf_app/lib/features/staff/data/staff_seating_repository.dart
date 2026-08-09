import 'dart:typed_data';

import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/staff/data/staff_endpoints.dart';
import 'package:simf_app/features/staff/data/staff_seating_models.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// D-771 — data layer for the staff seating desk. Three reads, all gated
/// server-side on `Seating.Assist` (an operational permission the Staff app role
/// carries — no admin RBAC role required).
class StaffSeatingRepository {
  StaffSeatingRepository(this._client);

  final SimfApiClient _client;

  /// `POST /app/staff/sessions/{id}/seating/by-badge` — where does the guest
  /// behind this badge QR sit? Throws [ApiFailure] `ATTENDEE_QR_UNKNOWN` when the
  /// badge is not recognised; a valid badge with no seat returns `found: false`.
  Future<StaffSeatOccupant> lookupByBadge(String sessionId, String qrId) {
    return _client.post<StaffSeatOccupant>(
      StaffEndpoints.seatingByBadge(sessionId),
      body: <String, dynamic>{'qrId': qrId},
      decodeData: _decode,
    );
  }

  /// `GET /app/staff/sessions/{id}/seating/seat` — who sits in this seat? A free
  /// seat returns `found: false` (a valid answer, not an error).
  Future<StaffSeatOccupant> lookupSeat(
    String sessionId, {
    required String rowLabel,
    required int seatNumber,
  }) {
    return _client.get<StaffSeatOccupant>(
      StaffEndpoints.seatingSeat(sessionId),
      queryParameters: <String, dynamic>{
        'rowLabel': rowLabel,
        'seatNumber': seatNumber,
      },
      decodeData: _decode,
    );
  }

  /// `GET /app/staff/sessions/{id}/seating/occupant/{userId}/photo` — the guest's
  /// photo bytes. D-422: fetched through the authenticated Dio bytes path, NEVER
  /// an `Image.network` (which cannot carry the bearer token). Returns null when
  /// there is no photo (404) rather than throwing, so the card falls back to the
  /// placeholder avatar.
  Future<Uint8List?> occupantPhoto(String sessionId, String userId) async {
    try {
      return await _client.getBytes(
        StaffEndpoints.seatingOccupantPhoto(sessionId, userId),
      );
    } on ApiFailure {
      return null;
    }
  }

  static StaffSeatOccupant _decode(Object? data) => StaffSeatOccupant.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      );
}

final staffSeatingRepositoryProvider = Provider<StaffSeatingRepository>((ref) {
  return StaffSeatingRepository(ref.watch(simfApiClientProvider));
});
