import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/moderation/data/moderation_models.dart';

void main() {
  group('ModeratorQuestion.fromJson (D-405 wire contract)', () {
    test('parses the moderator row fields', () {
      final q = ModeratorQuestion.fromJson(<String, dynamic>{
        'id': 'q1',
        'sessionId': 's1',
        'submittedByDisplayName': 'Raed Al-Salem',
        'questionText': 'How is AI used for cyber security?',
        'recipient': 1,
        'order': 3,
        'isHidden': false,
        'isPushed': true,
        'pushedAt': '2026-01-01T10:21:00Z',
        'createdAt': '2026-01-01T10:00:00Z',
      });

      expect(q.id, 'q1');
      expect(q.submitterName, 'Raed Al-Salem');
      expect(q.recipient, QuestionRecipient.host);
      expect(q.order, 3);
      expect(q.isPushed, isTrue);
      expect(q.isOnStage, isTrue);
      expect(q.createdAt.isUtc, isTrue);
    });

    test('defaults missing fields safely (speaker, not pushed)', () {
      final q = ModeratorQuestion.fromJson(const <String, dynamic>{});
      expect(q.recipient, QuestionRecipient.speaker);
      expect(q.isPushed, isFalse);
      expect(q.isOnStage, isFalse);
    });

    test('listFromData maps a bare list', () {
      final list = ModeratorQuestion.listFromData(<dynamic>[
        <String, dynamic>{'id': 'a', 'isPushed': false},
        <String, dynamic>{'id': 'b', 'isPushed': true},
      ]);
      expect(list, hasLength(2));
      expect(list[1].isOnStage, isTrue);
    });
  });

  group('filterModeratorQueue', () {
    final all = <ModeratorQuestion>[
      ModeratorQuestion.fromJson(<String, dynamic>{'id': 'a', 'isPushed': false}),
      ModeratorQuestion.fromJson(<String, dynamic>{'id': 'b', 'isPushed': true}),
      ModeratorQuestion.fromJson(<String, dynamic>{'id': 'c', 'isPushed': false}),
    ];

    test('all returns everything', () {
      expect(filterModeratorQueue(all, ModeratorQueueFilter.all), hasLength(3));
    });

    test('fresh = not on stage', () {
      final fresh = filterModeratorQueue(all, ModeratorQueueFilter.fresh);
      expect(fresh.map((q) => q.id), <String>['a', 'c']);
    });

    test('onStage = pushed', () {
      final onStage = filterModeratorQueue(all, ModeratorQueueFilter.onStage);
      expect(onStage.map((q) => q.id), <String>['b']);
    });
  });
}
