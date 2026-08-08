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
