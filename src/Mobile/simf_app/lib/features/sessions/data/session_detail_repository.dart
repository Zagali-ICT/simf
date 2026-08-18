import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/seat_map_repository.dart';
import 'package:simf_app/features/sessions/data/session_detail_eligibility.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/sessions_endpoints.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

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
      SessionsEndpoints.detail(sessionId),
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
      SessionsEndpoints.seats(sessionId),
      decodeData: MySeat.fromSeatMap,
    );
  }
}

final sessionDetailRepositoryProvider =
    Provider<SessionDetailRepository>((ref) {
  return SessionDetailRepository(ref.watch(simfApiClientProvider));
});

@immutable
class SessionDetailView {
  const SessionDetailView({
    required this.detail,
    required this.seatMap,
    required this.seatMapFailed,
  });

  final SessionDetail detail;
  final SessionSeatMap? seatMap;

  /// #18 — true when an approved signed-in account's seat-map fetch FAILED, so
  /// [seatMap] is null because it failed and not because a guest / pending
  /// account cannot join. Drives the join area's error+retry, so the Join
  /// affordance is never silently absent.
  final bool seatMapFailed;
}

/// One session's detail view, or **null when the server has no such id** (404).
final sessionDetailViewProvider = FutureProvider.autoDispose
    .family<SessionDetailView?, String>((ref, sessionId) async {
  try {
    final detail =
        await ref.watch(sessionDetailRepositoryProvider).getDetail(sessionId);
    // DEF-MOD-004 — the join / my-seat affordances open the attendee-only
    // routes (#18 my seat, #109 seat picker), so only an attendee's seat map is
    // fetched: a guest / pending account has no join section (L-3), and a Staff
    // / Moderator is not offered one either — the router would bounce them Home
    // the moment they tapped it.
    final canJoin = canJoinSession(roleOf(ref.watch(authControllerProvider)));
    SessionSeatMap? seatMap;
    if (canJoin) {
      try {
        seatMap = await ref
            .watch(seatMapRepositoryProvider)
            .getSeatMap(sessionId);
      } on ApiFailure {
        // 401 (no token) / 403 (not approved) / transport → no join section.
        seatMap = null;
      }
    }
    return SessionDetailView(
      detail: detail,
      seatMap: seatMap,
      // A null map for an attendee means the fetch FAILED (a success always
      // returns one), so the body can show a retry instead of silently
      // dropping the Join button.
      seatMapFailed: canJoin && seatMap == null,
    );
  } on ApiFailure catch (failure) {
    if (failure.httpStatus == 404) {
      return null;
    }
    rethrow;
  }
});
