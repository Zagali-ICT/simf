import 'package:flutter/foundation.dart';

/// Who a question is addressed to — mirrors `SessionQuestionRecipient`
/// (Speaker=0, Host=1).
enum QuestionRecipient { speaker, host }

/// The moderator-desk view of a question — mirrors the public JSON of
/// `SessionQuestionModeratorRow` (`GET /app/sessions/{id}/questions/moderate`).
/// The endpoint returns only **Approved** (non-hidden) rows, so a hidden
/// (rejected) question simply drops out of the list on the next read.
@immutable
class ModeratorQuestion {
  const ModeratorQuestion({
    required this.id,
    required this.sessionId,
    required this.submitterName,
    required this.questionText,
    required this.recipient,
    required this.order,
    required this.isHidden,
    required this.isPushed,
    required this.createdAt,
    this.pushedAt,
  });

  final String id;
  final String sessionId;
  final String submitterName;
  final String questionText;
  final QuestionRecipient recipient;
  final int order;
  final bool isHidden;
  final bool isPushed;
  final DateTime createdAt;
  final DateTime? pushedAt;

  /// Backend-faithful state for the filter chips: a pushed question is **on
  /// stage** (يتم الإجابة); otherwise it is **new** (جديد). (The Figma's
  /// distinct "answered / تمت الإجابة" state has no backend status — D-405.)
  bool get isOnStage => isPushed;

  static ModeratorQuestion fromJson(Map<String, dynamic> json) =>
      ModeratorQuestion(
        id: json['id'] as String? ?? '',
        sessionId: json['sessionId'] as String? ?? '',
        submitterName: json['submittedByDisplayName'] as String? ?? '',
        questionText: json['questionText'] as String? ?? '',
        recipient: (json['recipient'] as num?)?.toInt() == 1
            ? QuestionRecipient.host
            : QuestionRecipient.speaker,
        order: (json['order'] as num?)?.toInt() ?? 0,
        isHidden: json['isHidden'] as bool? ?? false,
        isPushed: json['isPushed'] as bool? ?? false,
        createdAt: _utc(json['createdAt']),
        pushedAt: json['pushedAt'] == null ? null : _utc(json['pushedAt']),
      );

  /// The endpoint returns a bare `ApiResult<IReadOnlyList<...>>`, so the data
  /// is the list itself.
  static List<ModeratorQuestion> listFromData(Object? data) =>
      (data as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => ModeratorQuestion.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);
}

/// The filter chips on the desk (Figma 758:5307, backend-faithful subset).
enum ModeratorQueueFilter { all, fresh, onStage }

DateTime _utc(Object? value) {
  if (value is String && value.isNotEmpty) {
    final parsed = DateTime.tryParse(value);
    if (parsed != null) {
      return parsed.toUtc();
    }
  }
  return DateTime.fromMillisecondsSinceEpoch(0, isUtc: true);
}

/// Applies a chip filter to the queue (pure — unit-testable).
List<ModeratorQuestion> filterModeratorQueue(
  List<ModeratorQuestion> all,
  ModeratorQueueFilter filter,
) {
  switch (filter) {
    case ModeratorQueueFilter.all:
      return all;
    case ModeratorQueueFilter.fresh:
      return all.where((q) => !q.isOnStage).toList(growable: false);
    case ModeratorQueueFilter.onStage:
      return all.where((q) => q.isOnStage).toList(growable: false);
  }
}
