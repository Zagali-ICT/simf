import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'moderation_models.dart';

/// Data layer for the per-session moderator Q&A desk (Figma 758:5307, D-405).
///
/// Every endpoint authorizes on the **per-session `SessionModerator` grant** (or
/// Administrator) — NOT on the mobile `AppRole.moderator`. A moderator without a
/// grant for the session gets **403**, which the screen renders as a "not
/// authorised" state (D-405).
class ModerationRepository {
  ModerationRepository(this._client);

  final SimfApiClient _client;

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
      '/app/sessions/$sessionId/questions/moderate$query',
      decodeData: ModeratorQuestion.listFromData,
    );
  }

  /// `PUT …/{questionId}/push` — mark the question on stage (يتم الإجابة).
  /// Idempotent server-side.
  Future<ModeratorQuestion> push(String sessionId, String questionId) {
    return _client.put<ModeratorQuestion>(
      '/app/sessions/$sessionId/questions/$questionId/push',
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
      '/app/sessions/$sessionId/questions/$questionId/hide',
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
      '/app/sessions/$sessionId/questions/$questionId/answered',
      body: <String, dynamic>{'isAnswered': isAnswered},
      decodeData: (data) => ModeratorQuestion.fromJson(_asMap(data)),
    );
  }

  static Map<String, dynamic> _asMap(Object? data) =>
      (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
}

final moderationRepositoryProvider = Provider<ModerationRepository>((ref) {
  return ModerationRepository(ref.watch(simfApiClientProvider));
});
