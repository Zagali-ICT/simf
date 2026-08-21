import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/moderation/data/moderation_models.dart';

/// `ModeratorQuestion.fromJson` is tolerant, so a row that OMITS `isHidden`
/// decodes to the fallback rather than throwing.
///
/// `isHidden` is the rejected/restored state of an audience question
/// (DEF-MOD-002 — `PUT …/{questionId}/hide` carries the same key back). It
/// decides whether a question is suppressed from the room, so defaulting it
/// TRUE would hide questions the moderator never rejected — a row the desk
/// cannot account for, on the wrong side of a decision nobody made.
///
/// The existing `moderation_models_test.dart` decodes `{}` and asserts the
/// recipient, push and status defaults but never `isHidden`, which is exactly
/// the gap this file closes.
void main() {
  group('ModeratorQuestion — isHidden defaults to VISIBLE', () {
    test('a rejected row decodes true, not to a fallback', () {
      final question = ModeratorQuestion.fromJson(const <String, dynamic>{
        'id': 'q1',
        'isHidden': true,
      });

      expect(question.isHidden, isTrue);
    });

    test('an ABSENT isHidden leaves the question visible', () {
      final question = ModeratorQuestion.fromJson(const <String, dynamic>{
        'id': 'q1',
        'questionText': 'How is AI used for cyber security?',
        'isPushed': true,
      });

      expect(question.isHidden, isFalse);
      // The row is still the desk's working bucket, so a missing key must not
      // read as a rejection from either direction.
      expect(question.isRejected, isFalse);
      expect(question.status, ModeratorQuestionStatus.approved);
    });

    test('an explicitly NULL isHidden leaves the question visible', () {
      final question = ModeratorQuestion.fromJson(const <String, dynamic>{
        'id': 'q1',
        'isHidden': null,
      });

      expect(question.isHidden, isFalse);
    });

    test('an empty row is visible and not pushed', () {
      final question = ModeratorQuestion.fromJson(const <String, dynamic>{});

      expect(question.isHidden, isFalse);
      expect(question.isPushed, isFalse);
    });

    test('withStatus recomputes isHidden from the persisted status', () {
      final visible = ModeratorQuestion.fromJson(const <String, dynamic>{
        'id': 'q1',
      });

      expect(visible.withStatus(ModeratorQuestionStatus.hidden).isHidden,
          isTrue,);
      expect(
        visible.withStatus(ModeratorQuestionStatus.approved).isHidden,
        isFalse,
      );
    });
  });
}
