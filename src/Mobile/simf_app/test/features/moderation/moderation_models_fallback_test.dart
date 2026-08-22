import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/moderation/data/moderation_models.dart';

/// DEF-MOD-002: an omitted `isHidden` must default to FALSE — defaulting it
/// true suppresses questions no moderator ever rejected.
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
