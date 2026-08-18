import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/live/data/live_models.dart';
import 'package:simf_app/features/sessions/data/sessions_endpoints.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Data layer for the Live broadcast screen (Page_025). One **anonymous** read
/// reusing the shipped public detail endpoint (no new API — D-271): decodes
/// only the broadcast slice into [LiveSession]. Throws [ApiFailure] on a wire
/// error — the screen maps a 404 to "not found" and any other failure to retry.
class LiveRepository {
  LiveRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/programme/sessions/{id}` → the live broadcast slice (E2).
  Future<LiveSession> getLiveSession(String sessionId) {
    return _client.get<LiveSession>(
      SessionsEndpoints.detail(sessionId),
      decodeData: (data) => LiveSession.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  }

  /// D-433 — the "الجلسات القادمة" cards: reuse the shipped agenda list
  /// (`GET /app/programme/sessions`, anonymous), keep only sessions starting in
  /// the future, soonest first, and excluding [excludeSessionId] (the one being
  /// watched). Returns at most [take].
  Future<List<UpcomingSession>> getUpcomingSessions({
    String? excludeSessionId,
    int take = 3,
  }) {
    return _client.get<List<UpcomingSession>>(
      SessionsEndpoints.programme,
      decodeData: (data) {
        final items = (data is Map ? data['items'] : null) as List? ??
            const <dynamic>[];
        final now = saudiNow();
        final upcoming = items
            .whereType<Map<dynamic, dynamic>>()
            .map((e) => UpcomingSession.fromJson(e.cast<String, dynamic>()))
            .where((s) =>
                s.id != excludeSessionId &&
                s.start != null &&
                s.start!.isAfter(now),)
            .toList()
          ..sort((a, b) => a.start!.compareTo(b.start!));
        return upcoming.take(take).toList(growable: false);
      },
    );
  }
}

final liveRepositoryProvider = Provider<LiveRepository>((ref) {
  return LiveRepository(ref.watch(simfApiClientProvider));
});

/// One live session, or **null when the server has no such id** (a 404).
///
/// The fold-to-null shape: the screen answers a missing session with its own
/// not-found copy rather than the error surface.
///
/// Only ever watched behind the login gate and the has-an-id check — a guest
/// sees the need-login prompt and the id-less global feed never reads a
/// session, so neither path may start this request.
final liveSessionProvider =
    FutureProvider.autoDispose.family<LiveSession?, String>((ref, id) async {
  try {
    return await ref.watch(liveRepositoryProvider).getLiveSession(id);
  } on ApiFailure catch (failure) {
    if (failure.httpStatus == 404) {
      return null;
    }
    rethrow;
  }
});

/// D-433 — the "الجلسات القادمة" strip.
///
/// Optional chrome, so a failure yields an EMPTY list rather than an error:
/// a list failure must not break the live screen, which is exactly what the
/// old non-blocking second read said by swallowing its own `ApiFailure`.
final upcomingSessionsProvider = FutureProvider.autoDispose
    .family<List<UpcomingSession>, String?>((ref, excludeId) async {
  try {
    return await ref
        .watch(liveRepositoryProvider)
        .getUpcomingSessions(excludeSessionId: excludeId);
  } on ApiFailure {
    return const <UpcomingSession>[];
  }
});
