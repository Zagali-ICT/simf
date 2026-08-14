import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/sessions_endpoints.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// App-local data layer for the sessions list (Page_016). The read is
/// **public** (`AllowAnonymous`) — a Guest sees the full programme. The app
/// fetches the whole programme **once, with no `day` filter**, and the UI
/// filters it inline (Page_016 L-1); the server's optional `?day=` is
/// intentionally unused. Throws [ApiFailure] on a wire error.
class SessionsRepository {
  SessionsRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/programme/sessions` → the whole active programme (envelope
  /// `PublicSessions = { items: [...] }`), time-ordered (L-5).
  Future<List<SessionListItem>> getSessions() {
    return _client.get<List<SessionListItem>>(
      SessionsEndpoints.programme,
      decodeData: (data) => SessionsPage.fromJson(data).items,
    );
  }

  /// `GET /app/programme/days` (D-452) → the day-grouped programme (envelope
  /// `PublicProgrammeDays = { days: [...] }`): each day with its bilingual
  /// title, a has-logo flag, and the day's sessions. Drives the Sessions
  /// screen's day banner ("تفاصيل اليوم"), the day strip and the type tabs.
  Future<List<ProgrammeDay>> getDays() {
    return _client.get<List<ProgrammeDay>>(
      SessionsEndpoints.programmeDays,
      decodeData: (data) => ProgrammeDaysPage.fromJson(data).days,
    );
  }
}

final sessionsRepositoryProvider = Provider<SessionsRepository>((ref) {
  return SessionsRepository(ref.watch(simfApiClientProvider));
});

/// The whole active programme as one cached list (`GET /app/programme/sessions`)
/// — the single source screens read `SessionListItem` state from (phase,
/// `hasPublishedSummary`). The session-summaries list, the AI-summary screen,
/// the join hub and the presentations screen all watch this one provider (the
/// former per-screen `getSessions()` copies were collapsed onto it).
final programmeSessionsProvider =
    FutureProvider.autoDispose<List<SessionListItem>>(
  (ref) => ref.watch(sessionsRepositoryProvider).getSessions(),
);

/// The active programme keyed by session id, derived once per programme change
/// rather than per rebuild.
///
/// The presentations screen built this literal inside `build()`, so every
/// keystroke on its day tabs re-walked the whole programme to produce a map it
/// then threw away. A derived provider recomputes only when
/// [programmeSessionsProvider] itself changes.
final programmeSessionsByIdProvider =
    Provider.autoDispose<Map<String, SessionListItem>>((ref) {
  final sessions =
      ref.watch(programmeSessionsProvider).valueOrNull ??
      const <SessionListItem>[];
  return <String, SessionListItem>{
    for (final session in sessions) session.id: session,
  };
});
