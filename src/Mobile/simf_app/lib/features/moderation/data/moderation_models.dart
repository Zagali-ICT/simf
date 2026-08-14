import 'package:flutter/foundation.dart';
import 'package:simf_app/core/utils/saudi_time.dart';

/// Who a question is addressed to — mirrors `SessionQuestionRecipient`
/// (Speaker=0, Host=1).
enum QuestionRecipient { speaker, host }

/// The persisted pipeline state of a question — mirrors `QuestionStatus`
/// (Pending=0, Approved=1, Hidden=2, Answered=3).
///
/// DEF-MOD-001 — `answered` is a REAL backend status now, not a screen-local
/// marker: the "تمت الإجابة" mark survives leaving the desk, an app restart and
/// a co-moderator working the same session on another device.
enum ModeratorQuestionStatus {
  pending,
  approved,
  hidden,
  answered;

  static ModeratorQuestionStatus fromWire(Object? raw) {
    switch ((raw as num?)?.toInt()) {
      case 0:
        return ModeratorQuestionStatus.pending;
      case 2:
        return ModeratorQuestionStatus.hidden;
      case 3:
        return ModeratorQuestionStatus.answered;
      default:
        // 1 = Approved, and the desk's default bucket for an older wire shape
        // that did not carry `status` at all.
        return ModeratorQuestionStatus.approved;
    }
  }

  /// The value the API binds for `?status=` on the moderate list.
  String get wireName {
    switch (this) {
      case ModeratorQuestionStatus.pending:
        return 'Pending';
      case ModeratorQuestionStatus.approved:
        return 'Approved';
      case ModeratorQuestionStatus.hidden:
        return 'Hidden';
      case ModeratorQuestionStatus.answered:
        return 'Answered';
    }
  }
}

/// The moderator-desk view of a question — mirrors the public JSON of
/// `SessionQuestionModeratorRow` (`GET /app/sessions/{id}/questions/moderate`).
/// Omitting `?status` returns the working desk (Approved + Answered);
/// `?status=Hidden` returns the rejected rows so the desk can restore one
/// (DEF-MOD-002).
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
    required this.status,
    this.pushedAt,
  });

  factory ModeratorQuestion.fromJson(Map<String, dynamic> json) =>
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
        status: ModeratorQuestionStatus.fromWire(json['status']),
        pushedAt: json['pushedAt'] == null ? null : _utc(json['pushedAt']),
      );

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
  final ModeratorQuestionStatus status;

  /// A pushed question that has not yet been answered is **on stage**
  /// (يتم الإجابة); otherwise it is **new** (جديد).
  bool get isOnStage => isPushed && !isAnswered;

  /// DEF-MOD-001 — تمت الإجابة, read from the persisted status.
  bool get isAnswered => status == ModeratorQuestionStatus.answered;

  /// DEF-MOD-002 — مرفوض, read from the persisted status.
  bool get isRejected => status == ModeratorQuestionStatus.hidden;

  /// The same row with a different persisted status — used for the desk's
  /// optimistic update before the server confirms (rolled back on failure).
  ModeratorQuestion withStatus(ModeratorQuestionStatus next) => ModeratorQuestion(
        id: id,
        sessionId: sessionId,
        submitterName: submitterName,
        questionText: questionText,
        recipient: recipient,
        order: order,
        isHidden: next == ModeratorQuestionStatus.hidden,
        isPushed: isPushed,
        createdAt: createdAt,
        status: next,
        pushedAt: pushedAt,
      );

  /// The endpoint returns a bare `ApiResult<IReadOnlyList<...>>`, so the data
  /// is the list itself.
  static List<ModeratorQuestion> listFromData(Object? data) =>
      (data as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => ModeratorQuestion.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);
}

/// FR-MOD-001 — one session the signed-in user actually moderates. Mirrors the
/// public JSON of `ModeratedSessionRow` (`GET /app/sessions/moderated`).
///
/// The Q&A desk is authorised **per session**, so the app used to offer the
/// forum action on EVERY session and let the missing grant surface as a 403
/// after the tap. This is the discovery list behind that affordance: the
/// session-detail action is now only offered where the grant exists, and the
/// moderator's operational home lists these sessions so they can navigate
/// straight to their own desks.
@immutable
class ModeratedSession {
  const ModeratedSession({
    required this.sessionId,
    required this.title,
    required this.titleArabic,
    required this.hallName,
    required this.hallNameArabic,
    required this.start,
    required this.end,
  });

  factory ModeratedSession.fromJson(Map<String, dynamic> json) => ModeratedSession(
        sessionId: json['sessionId'] as String? ?? '',
        title: json['title'] as String? ?? '',
        titleArabic: json['titleArabic'] as String? ?? '',
        hallName: json['hallName'] as String? ?? '',
        hallNameArabic: json['hallNameArabic'] as String? ?? '',
        start: _utc(json['start']),
        end: _utc(json['end']),
      );

  final String sessionId;
  final String title;
  final String titleArabic;
  final String hallName;
  final String hallNameArabic;
  final DateTime start;
  final DateTime end;

  String localizedTitle({required bool isArabic}) =>
      _pick(titleArabic, title, isArabic);

  String localizedHall({required bool isArabic}) =>
      _pick(hallNameArabic, hallName, isArabic);

  static String _pick(String arabic, String english, bool isArabic) {
    final preferred = isArabic ? arabic : english;
    if (preferred.trim().isNotEmpty) {
      return preferred;
    }
    return isArabic ? english : arabic;
  }

  static List<ModeratedSession> listFromData(Object? data) =>
      (data as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => ModeratedSession.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);
}

/// The five filter chips on the desk (Figma 1461:12227).
///
/// DEF-MOD-001 / DEF-MOD-002 — every chip is now backed by the PERSISTED
/// `status`: [fresh] / [accepted] are the Approved rows split by the push flag,
/// [answered] are the Answered rows, and [rejected] are the Hidden rows the desk
/// pulls with `?status=Hidden`. Nothing lives only in screen state any more, so
/// leaving the desk (or a co-moderator on another device) no longer loses work.
enum ModeratorQueueFilter { all, fresh, accepted, answered, rejected }

DateTime _utc(Object? value) {
  if (value is String && value.isNotEmpty) {
    final parsed = parseWireOrNull(value);
    if (parsed != null) {
      return parsed;
    }
  }
  return DateTime.fromMillisecondsSinceEpoch(0, isUtc: true);
}

/// Applies a chip filter to the desk (pure — unit-testable). [desk] is the
/// working queue from the server (Approved + Answered); [rejected] is the
/// separately fetched Hidden bucket.
List<ModeratorQuestion> filterModeratorQueue(
  List<ModeratorQuestion> desk,
  ModeratorQueueFilter filter, {
  List<ModeratorQuestion> rejected = const <ModeratorQuestion>[],
}) {
  switch (filter) {
    case ModeratorQueueFilter.all:
      return <ModeratorQuestion>[...desk, ...rejected];
    case ModeratorQueueFilter.fresh:
      return desk
          .where((q) => !q.isAnswered && !q.isOnStage)
          .toList(growable: false);
    case ModeratorQueueFilter.accepted:
      return desk.where((q) => q.isOnStage).toList(growable: false);
    case ModeratorQueueFilter.answered:
      return desk.where((q) => q.isAnswered).toList(growable: false);
    case ModeratorQueueFilter.rejected:
      return rejected;
  }
}
