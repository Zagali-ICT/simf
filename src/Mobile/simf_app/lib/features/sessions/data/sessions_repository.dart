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

  /// `GET /app/programme/days` (D-452) → the day-grouped programme (envelope
  /// `PublicProgrammeDays = { days: [...] }`): each day with its bilingual
  /// title, a has-logo flag, and the day's sessions. Drives the Sessions
  /// screen's day banner ("تفاصيل اليوم"), the day strip and the type tabs.
  Future<List<ProgrammeDay>> getDays() {
    return _client.get<List<ProgrammeDay>>(
      '/app/programme/days',
      decodeData: (data) => ProgrammeDaysPage.fromJson(data).days,
    );
  }
}

final sessionsRepositoryProvider = Provider<SessionsRepository>((ref) {
  return SessionsRepository(ref.watch(simfApiClientProvider));
});

/// The whole active programme as one cached list (`GET /app/programme/sessions`)
/// — the single source screens read `SessionListItem` state from (phase,
/// `hasPublishedSummary`). New readers watch this instead of re-declaring the
/// same `getSessions()` wrapper. (The pre-existing `aiSummarySessionsProvider`
/// + `joinHubSessionsProvider` are identical copies kept for now — collapse them
/// onto this one in a separate DRY pass.)
final programmeSessionsProvider =
    FutureProvider.autoDispose<List<SessionListItem>>(
  (ref) => ref.watch(sessionsRepositoryProvider).getSessions(),
);
