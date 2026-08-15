import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:share_plus/share_plus.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/sessions_endpoints.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Data layer for the My-Seat map (Page_018). One read reuses the shipped
/// seat endpoint (no new API — D-267); `RequireApprovedAccount` (the route is
/// auth-gated). Throws [ApiFailure] on a wire error — the screen maps 404 to
/// "session removed" (L-6).
class SeatMapRepository {
  SeatMapRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/sessions/{id}/seats` → the whole grid + `myCell` (E1).
  Future<SessionSeatMap> getSeatMap(String sessionId) {
    return _client.get<SessionSeatMap>(
      SessionsEndpoints.seats(sessionId),
      decodeData: (data) => SessionSeatMap.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  }

  /// D-485 — `POST /app/sessions/{id}/seats/reserve` → hold a specific seat
  /// (assigned-seat session). The reservation is created Pending; the Control
  /// Panel approves it (the approved/rejected notification arrives in the inbox).
  /// Throws [ApiFailure] on a wire error (409 seat-taken / session-full /
  /// already-booked / open-seating-only).
  Future<MyReservation> reserveSeat(
    String sessionId, {
    required String rowLabel,
    required int seatNumber,
  }) {
    return _client.post<MyReservation>(
      SessionsEndpoints.reserveSeat(sessionId),
      body: <String, dynamic>{'rowLabel': rowLabel, 'seatNumber': seatNumber},
      decodeData: _decodeReservation,
    );
  }

  /// D-485 — `POST /app/sessions/{id}/seats/reserve-random` → the system holds
  /// the first free seat (assigned-seat session), created Pending.
  Future<MyReservation> reserveRandom(String sessionId) {
    return _client.post<MyReservation>(
      SessionsEndpoints.reserveRandomSeat(sessionId),
      // Send an empty JSON object, not a null body: the endpoint rejects a
      // bodyless POST with 400 VALIDATION_FAILED ("input does not contain any
      // JSON tokens") before it ever reaches the handler.
      body: const <String, dynamic>{},
      decodeData: _decodeReservation,
    );
  }

  /// D-485 — `POST /app/sessions/{id}/seats/join` → join an open-seating
  /// (general-admission) session with no specific seat, created Pending. A 409
  /// `SEAT_SELECTION_REQUIRED` means the session actually needs a seat pick.
  Future<MyReservation> joinOpenSeating(String sessionId) {
    return _client.post<MyReservation>(
      SessionsEndpoints.joinSeat(sessionId),
      // Empty JSON object, not a null body — a bodyless POST is rejected with
      // 400 VALIDATION_FAILED before the handler runs (see reserveRandom).
      body: const <String, dynamic>{},
      decodeData: _decodeReservation,
    );
  }

  /// B1 — `POST /app/sessions/{id}/seats/move` → change the caller's
  /// already-held seat to [rowLabel]+[seatNumber] in ONE atomic step (the
  /// server acquires the destination and releases the source in a single
  /// transaction, so a lost race leaves the original seat held). Throws
  /// [ApiFailure] on 409 `SEAT_ALREADY_RESERVED` (someone took it first) /
  /// `SEAT_MOVE_SAME_SEAT` / `SEAT_TIER_RESERVED` / `SEAT_TIER_NOT_ELIGIBLE` /
  /// `BOOKING_SESSION_STARTED`, or 404 `SEAT_RESERVATION_NOT_FOUND` when there
  /// is no seat to move.
  Future<MyReservation> moveSeat(
    String sessionId, {
    required String rowLabel,
    required int seatNumber,
  }) {
    return _client.post<MyReservation>(
      SessionsEndpoints.moveSeat(sessionId),
      body: <String, dynamic>{'rowLabel': rowLabel, 'seatNumber': seatNumber},
      decodeData: _decodeReservation,
    );
  }

  /// D-485 — `DELETE /app/sessions/{id}/seats/mine` → cancel the caller's own
  /// held reservation (before the session starts). A 404 means there was none.
  Future<void> releaseMine(String sessionId) {
    return _client.delete<bool>(
      SessionsEndpoints.mySeat(sessionId),
      decodeData: (_) => true,
    );
  }

  static MyReservation _decodeReservation(Object? data) =>
      MyReservation.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      );
}

final seatMapRepositoryProvider = Provider<SeatMapRepository>((ref) {
  return SeatMapRepository(ref.watch(simfApiClientProvider));
});

/// The seat map for `sessionId` as one cached async read — the single source
/// the My-Seat view (#18) and the Seat-picker (#109) both watch (`GET
/// …/seats`). `autoDispose.family` so a fresh mount or a retry
/// (`ref.invalidate`) re-fetches, matching the per-mount `initState` load the
/// two screens used before.
final seatMapProvider =
    FutureProvider.autoDispose.family<SessionSeatMap, String>(
  (ref, sessionId) =>
      ref.watch(seatMapRepositoryProvider).getSeatMap(sessionId),
);

/// The native "share my seat location" action (Page_018 E3) — a client-local OS
/// action, kept behind an overridable provider so the widget test injects a
/// fake (no MethodChannel).
class SeatShare {
  const SeatShare();

  Future<void> shareText(String text) async {
    await Share.share(text);
  }
}

final seatShareProvider = Provider<SeatShare>((ref) => const SeatShare());
