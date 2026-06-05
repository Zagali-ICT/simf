import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'session_models.dart';

/// App-local data layer for the sessions list (Page_016). The read is **public**
/// (`AllowAnonymous`) — a Guest sees the full programme. The app fetches the
/// whole programme **once, with no `day` filter**, and the UI filters it inline
/// (Page_016 L-1); the server's optional `?day=` is intentionally unused.
/// Throws [ApiFailure] on a wire error.
class SessionsRepository {
  SessionsRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/programme/sessions` → the whole active programme (envelope
  /// `PublicSessions = { items: [...] }`), time-ordered (L-5).
  Future<List<SessionListItem>> getSessions() {
    return _client.get<List<SessionListItem>>(
      '/app/programme/sessions',
      decodeData: (data) => SessionsPage.fromJson(data).items,
    );
  }
}

final sessionsRepositoryProvider = Provider<SessionsRepository>((ref) {
  return SessionsRepository(ref.watch(simfApiClientProvider));
});
