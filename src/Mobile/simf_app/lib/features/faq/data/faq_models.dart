import 'package:flutter/foundation.dart';

/// One FAQ question/answer pair (`GET /app/faq` → groups[].entries[]). Bilingual;
/// the localized getters fall back to the other language when one side is blank.
@immutable
class FaqEntry {
  const FaqEntry({
    required this.id,
    required this.question,
    required this.questionArabic,
    required this.answer,
    required this.answerArabic,
  });

  factory FaqEntry.fromJson(Map<String, dynamic> json) => FaqEntry(
        id: (json['id'] ?? '').toString(),
        question: (json['question'] ?? '').toString(),
        questionArabic: (json['questionArabic'] ?? '').toString(),
        answer: (json['answer'] ?? '').toString(),
        answerArabic: (json['answerArabic'] ?? '').toString(),
      );

  final String id;
  final String question;
  final String questionArabic;
  final String answer;
  final String answerArabic;

  String localizedQuestion({required bool isArabic}) =>
      _pick(isArabic, questionArabic, question);
  String localizedAnswer({required bool isArabic}) =>
      _pick(isArabic, answerArabic, answer);
}

/// One FAQ group with its ordered active entries.
@immutable
class FaqGroup {
  const FaqGroup({
    required this.id,
    required this.name,
    required this.nameArabic,
    required this.entries,
  });

  factory FaqGroup.fromJson(Map<String, dynamic> json) => FaqGroup(
        id: (json['id'] ?? '').toString(),
        name: (json['name'] ?? '').toString(),
        nameArabic: (json['nameArabic'] ?? '').toString(),
        entries: ((json['entries'] as List?) ?? const <dynamic>[])
            .map(
              (e) => FaqEntry.fromJson((e as Map).cast<String, dynamic>()),
            )
            .toList(),
      );

  final String id;
  final String name;
  final String nameArabic;
  final List<FaqEntry> entries;

  String localizedName({required bool isArabic}) => _pick(isArabic, nameArabic, name);
}

/// Arabic-first/English-first pick with a fall back to the populated side.
String _pick(bool isArabic, String arabic, String english) {
  if (isArabic) {
    return arabic.trim().isNotEmpty ? arabic : english;
  }
  return english.trim().isNotEmpty ? english : arabic;
}
