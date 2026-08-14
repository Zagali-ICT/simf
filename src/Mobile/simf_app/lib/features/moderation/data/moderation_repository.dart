import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/moderation/data/moderation_endpoints.dart';
import 'package:simf_app/features/moderation/data/moderation_models.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Data layer for the per-session moderator Q&A desk (Figma 758:5307, D-405).
///
/// Every endpoint authorizes on the **per-session `SessionModerator` grant**
/// (or Administrator) — NOT on the mobile `AppRole.moderator`. A moderator
/// without a grant for the session gets **403**, which the screen renders as a
/// "not authorised" state (D-405).
class ModerationRepository {
  ModerationRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/sessions/moderated` — FR-MOD-001: the sessions the signed-in
  /// user actually holds a per-session grant on. Drives both the session-detail
  /// affordance (offer the desk only where the grant exists) and the
  /// moderator's operational home list.
  Future<List<ModeratedSession>> getMySessions() {
    return _client.get<List<ModeratedSession>>(
      ModerationEndpoints.moderatedSessions,
      decodeData: ModeratedSession.listFromData,
    );
  }

  /// `GET /app/sessions/{sessionId}/questions/moderate[?status=…]`.
  ///
  /// DEF-MOD-002 — omitting [status] returns the working desk (the
  /// Committee-approved rows plus the ones already marked answered);
  /// `status: hidden` returns the rejected rows so the desk can list — and
  /// restore — a mis-clicked reject instead of losing it for good.
  Future<List<ModeratorQuestion>> getQueue(
    String sessionId, {
    ModeratorQuestionStatus? status,
  }) {
    final query = status == null ? '' : '?status=${status.wireName}';
    return _client.get<List<ModeratorQuestion>>(
      ModerationEndpoints.moderateQuestions(sessionId, query),
      decodeData: ModeratorQuestion.listFromData,
    );
  }

  /// `PUT …/{questionId}/push` — mark the question on stage (يتم الإجابة).
  /// Idempotent server-side.
  Future<ModeratorQuestion> push(String sessionId, String questionId) {
    return _client.put<ModeratorQuestion>(
      ModerationEndpoints.pushQuestion(sessionId, questionId),
      decodeData: (data) => ModeratorQuestion.fromJson(_asMap(data)),
    );
  }

  /// `PUT …/{questionId}/hide` `{isHidden}` — reject (مرفوض) / restore. Hiding
  /// moves the row to the rejected bucket on the next read.
  Future<ModeratorQuestion> setHidden(
    String sessionId,
    String questionId, {
    required bool isHidden,
  }) {
    return _client.put<ModeratorQuestion>(
      ModerationEndpoints.hideQuestion(sessionId, questionId),
      body: <String, dynamic>{'isHidden': isHidden},
      decodeData: (data) => ModeratorQuestion.fromJson(_asMap(data)),
    );
  }

  /// `PUT …/{questionId}/answered` `{isAnswered}` — DEF-MOD-001: persist the
  /// تمت الإجابة mark. Idempotent server-side; only an approved question can be
  /// marked answered.
  Future<ModeratorQuestion> setAnswered(
    String sessionId,
    String questionId, {
    required bool isAnswered,
  }) {
    return _client.put<ModeratorQuestion>(
      ModerationEndpoints.answeredQuestion(sessionId, questionId),
      body: <String, dynamic>{'isAnswered': isAnswered},
      decodeData: (data) => ModeratorQuestion.fromJson(_asMap(data)),
    );
  }

  /// `PUT …/questions/reorder` `{orderedQuestionIds}` — FR-MOD-003: persist the
  /// desk's running order. The server replays `Order` 0..n-1 over the supplied
  /// ids and requires EVERY question on the working desk exactly once, so the
  /// caller sends the full desk in its new order, not just the moved row.
  Future<void> reorder(String sessionId, List<String> orderedQuestionIds) {
    return _client.put<bool>(
      ModerationEndpoints.reorderQuestions(sessionId),
      body: <String, dynamic>{'orderedQuestionIds': orderedQuestionIds},
      decodeData: (_) => true,
    );
  }

  static Map<String, dynamic> _asMap(Object? data) =>
      (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
}

final moderationRepositoryProvider = Provider<ModerationRepository>((ref) {
  return ModerationRepository(ref.watch(simfApiClientProvider));
});

/// FR-MOD-001 — the signed-in user's moderated sessions, cached for the
/// session-detail affordance gate and the moderator home list. `autoDispose` so
/// leaving both surfaces drops it and a fresh grant is picked up on the next
/// visit rather than being cached for the app's lifetime.
final myModeratedSessionsProvider =
    FutureProvider.autoDispose<List<ModeratedSession>>((ref) {
  return ref.watch(moderationRepositoryProvider).getMySessions();
});
