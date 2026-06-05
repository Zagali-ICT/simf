import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'session_models.dart';

/// Data layer for the session detail (Page_017). Two reads, both reusing the
/// shipped endpoints (no new API — D-265): the **anonymous** detail and the
/// **approved-only** seat map (for the my-seat card). Throws [ApiFailure] on a
/// wire error — the screen maps a 404 on the detail to "not found", and a
/// 401/403 on the seat read to "no card" (Page_017 L-3/L-7).
class SessionDetailRepository {
  SessionDetailRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/programme/sessions/{id}` → the full public detail (E2). 404
  /// (`SessionNotFound`) when the session is missing / soft-deleted.
  Future<SessionDetail> getDetail(String sessionId) {
    return _client.get<SessionDetail>(
      '/app/programme/sessions/$sessionId',
      decodeData: (data) => SessionDetail.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  }

  /// `GET /app/sessions/{id}/seats` → the caller's own seat (`myCell`), or null
  /// when they hold none (E3). Approved account only — a guest never calls it,
  /// and a 401/403 throws so the screen hides the card (L-3).
  Future<MySeat?> getMySeat(String sessionId) {
    return _client.get<MySeat?>(
      '/app/sessions/$sessionId/seats',
      decodeData: MySeat.fromSeatMap,
    );
  }
}

final sessionDetailRepositoryProvider = Provider<SessionDetailRepository>((ref) {
  return SessionDetailRepository(ref.watch(simfApiClientProvider));
});
