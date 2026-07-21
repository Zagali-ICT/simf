import 'package:flutter/foundation.dart';

/// One question on the dynamic rating form (a fixed 1–5 star scale).
@immutable
class RatingFormQuestion {
  const RatingFormQuestion({
    required this.id,
    required this.text,
    required this.textArabic,
    required this.isRequired,
  });

  final String id;
  final String text;
  final String textArabic;
  final bool isRequired;

  String localizedText(bool isArabic) {
    final ar = textArabic.trim();
    final en = text.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  static RatingFormQuestion fromJson(Map<String, dynamic> json) {
    return RatingFormQuestion(
      id: json['id'] as String? ?? '',
      text: json['text'] as String? ?? '',
      textArabic: json['textArabic'] as String? ?? '',
      isRequired: json['isRequired'] as bool? ?? false,
    );
  }
}

/// A group of questions on the form (e.g. Speaker / Sound / Light).
@immutable
class RatingFormGroup {
  const RatingFormGroup({
    required this.id,
    required this.name,
    required this.nameArabic,
    required this.questions,
  });

  final String id;
  final String name;
  final String nameArabic;
  final List<RatingFormQuestion> questions;

  String localizedName(bool isArabic) {
    final ar = nameArabic.trim();
    final en = name.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  static RatingFormGroup fromJson(Map<String, dynamic> json) {
    final rawQuestions = (json['questions'] as List?) ?? const <dynamic>[];
    return RatingFormGroup(
      id: json['id'] as String? ?? '',
      name: json['name'] as String? ?? '',
      nameArabic: json['nameArabic'] as String? ?? '',
      questions: rawQuestions
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => RatingFormQuestion.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false),
    );
  }
}

/// The attendee's existing submission, used to prefill the form.
@immutable
class RatingExistingSubmission {
  const RatingExistingSubmission({
    required this.overallStars,
    required this.comment,
    required this.answers,
  });

  final int? overallStars;
  final String? comment;

  /// questionId → stars.
  final Map<String, int> answers;

  static RatingExistingSubmission? fromJson(Object? raw) {
    if (raw is! Map) {
      return null;
    }
    final json = raw.cast<String, dynamic>();
    final rawAnswers = (json['answers'] as List?) ?? const <dynamic>[];
    final answers = <String, int>{};
    for (final a in rawAnswers.whereType<Map<dynamic, dynamic>>()) {
      final m = a.cast<String, dynamic>();
      final id = m['questionId'] as String?;
      final stars = (m['stars'] as num?)?.toInt();
      if (id != null && stars != null) {
        answers[id] = stars;
      }
    }
    return RatingExistingSubmission(
      overallStars: (json['overallStars'] as num?)?.toInt(),
      comment: json['comment'] as String?,
      answers: answers,
    );
  }
}

/// The full dynamic rating form for a type (+ optional target).
@immutable
class RatingFormView {
  const RatingFormView({
    required this.ratingTypeId,
    required this.code,
    required this.name,
    required this.nameArabic,
    required this.hasOverallStars,
    required this.allowComment,
    required this.commentLabel,
    required this.commentLabelArabic,
    required this.targetId,
    required this.groups,
    required this.ungroupedQuestions,
    required this.existing,
    this.targetName,
    this.targetNameArabic,
    this.targetStartUtc,
    this.isEligible = true,
  });

  final String ratingTypeId;
  final String code;
  final String name;
  final String nameArabic;
  final bool hasOverallStars;
  final bool allowComment;
  final String? commentLabel;
  final String? commentLabelArabic;
  final String? targetId;
  final List<RatingFormGroup> groups;
  final List<RatingFormQuestion> ungroupedQuestions;
  final RatingExistingSubmission? existing;

  /// D-713 (item 8) — the rated session's title + start time, for the "watched
  /// at {session} · {date}" header. Present only for a per-session rating.
  final String? targetName;
  final String? targetNameArabic;
  final DateTime? targetStartUtc;

  /// Owner 2026-07-19 — false when the caller has not attended what this type
  /// rates. The screen keeps the form visible but disables submit and shows an
  /// "attend to rate" note; the hard gate is server-side on submit. Append-only
  /// (D-219); defaults true so an older server (no field) reads as eligible.
  final bool isEligible;

  /// The rated session's title in the active locale, or null when there is no
  /// per-session target (a Global "App" rating).
  String? localizedTargetName(bool isArabic) {
    final ar = (targetNameArabic ?? '').trim();
    final en = (targetName ?? '').trim();
    final value = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
    return value.isEmpty ? null : value;
  }

  String? localizedCommentLabel(bool isArabic) {
    final ar = (commentLabelArabic ?? '').trim();
    final en = (commentLabel ?? '').trim();
    final value = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
    return value.isEmpty ? null : value;
  }

  static RatingFormView fromData(Object? data) {
    final json = (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
    final rawGroups = (json['groups'] as List?) ?? const <dynamic>[];
    final rawUngrouped = (json['ungroupedQuestions'] as List?) ?? const <dynamic>[];
    return RatingFormView(
      ratingTypeId: json['ratingTypeId'] as String? ?? '',
      code: json['code'] as String? ?? '',
      name: json['name'] as String? ?? '',
      nameArabic: json['nameArabic'] as String? ?? '',
      hasOverallStars: json['hasOverallStars'] as bool? ?? true,
      allowComment: json['allowComment'] as bool? ?? false,
      commentLabel: json['commentLabel'] as String?,
      commentLabelArabic: json['commentLabelArabic'] as String?,
      targetId: json['targetId'] as String?,
      groups: rawGroups
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => RatingFormGroup.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false),
      ungroupedQuestions: rawUngrouped
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => RatingFormQuestion.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false),
      existing: RatingExistingSubmission.fromJson(json['existing']),
      targetName: json['targetName'] as String?,
      targetNameArabic: json['targetNameArabic'] as String?,
      targetStartUtc: DateTime.tryParse(json['targetStartUtc'] as String? ?? ''),
      isEligible: json['isEligible'] as bool? ?? true,
    );
  }
}
