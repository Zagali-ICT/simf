import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:share_plus/share_plus.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'seat_map_models.dart';

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
      '/app/sessions/$sessionId/seats',
      decodeData: (data) => SessionSeatMap.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  }
}

final seatMapRepositoryProvider = Provider<SeatMapRepository>((ref) {
  return SeatMapRepository(ref.watch(simfApiClientProvider));
});

/// The native "share my seat location" action (Page_018 E3) — a client-local OS
/// action, kept behind an overridable provider so the widget test injects a fake
/// (no MethodChannel).
class SeatShare {
  const SeatShare();

  Future<void> shareText(String text) async {
    await Share.share(text);
  }
}

final seatShareProvider = Provider<SeatShare>((ref) => const SeatShare());
