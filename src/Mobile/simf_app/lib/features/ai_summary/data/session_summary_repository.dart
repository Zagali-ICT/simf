import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'ai_summary_endpoints.dart';
import 'session_summary_models.dart';

/// Data layer for the AI session summary (Page_034). One read, reusing the
/// shipped **anonymous** endpoint (no new API): the published summary for a
/// session. Throws [ApiFailure] on a wire error — the screen maps a 404
/// (`SessionSummaryNotFound`) to the "no published summary yet" state and any
/// other failure to error + retry.
class SessionSummaryRepository {
  SessionSummaryRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/programme/sessions/{id}/summary` → the published summary. 404
  /// when the session has no published summary (or is missing / soft-deleted).
  Future<SessionSummary> getSummary(String sessionId) {
    return _client.get<SessionSummary>(
      AiSummaryEndpoints.sessionSummary(sessionId),
      decodeData: SessionSummary.fromData,
    );
  }
}

final sessionSummaryRepositoryProvider =
    Provider<SessionSummaryRepository>((ref) {
  return SessionSummaryRepository(ref.watch(simfApiClientProvider));
});
