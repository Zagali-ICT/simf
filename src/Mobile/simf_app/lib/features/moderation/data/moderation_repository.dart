import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'moderation_models.dart';

/// Data layer for the per-session moderator Q&A desk (Figma 758:5307, D-405).
///
/// The four endpoints all authorize on the **per-session `SessionModerator`
/// grant** (or Administrator) — NOT on the mobile `AppRole.moderator`. A
/// moderator without a grant for the session gets **403**, which the screen
/// renders as a "not authorised" state (D-405; the grant pipeline is flagged
/// for the owner). The list returns only Approved (non-hidden) rows.
class ModerationRepository {
  ModerationRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/sessions/{sessionId}/questions/moderate` → the approved queue.
  Future<List<ModeratorQuestion>> getQueue(String sessionId) {
    return _client.get<List<ModeratorQuestion>>(
      '/app/sessions/$sessionId/questions/moderate',
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
  /// removes it from the approved queue on the next read.
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

  static Map<String, dynamic> _asMap(Object? data) =>
      (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
}

final moderationRepositoryProvider = Provider<ModerationRepository>((ref) {
  return ModerationRepository(ref.watch(simfApiClientProvider));
});
