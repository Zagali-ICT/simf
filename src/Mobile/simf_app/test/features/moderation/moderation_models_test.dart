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

  group('filterModeratorQueue (Figma 1461:12227, five chips)', () {
    ModeratorQuestion q(String id, {bool pushed = false}) =>
        ModeratorQuestion.fromJson(<String, dynamic>{'id': id, 'isPushed': pushed});
    final approved = <ModeratorQuestion>[
      q('a'),
      q('b', pushed: true),
      q('c'),
    ];

    test('all returns the live queue plus the rejected rows', () {
      expect(filterModeratorQueue(approved, ModeratorQueueFilter.all), hasLength(3));
    });

    test('fresh = approved, not on stage, not answered/rejected', () {
      final fresh = filterModeratorQueue(approved, ModeratorQueueFilter.fresh);
      expect(fresh.map((q) => q.id), <String>['a', 'c']);
    });

    test('accepted = on stage (pushed)', () {
      final accepted =
          filterModeratorQueue(approved, ModeratorQueueFilter.accepted);
      expect(accepted.map((q) => q.id), <String>['b']);
    });

    test('answered uses the session-local answered set, drops from fresh', () {
      expect(
        filterModeratorQueue(approved, ModeratorQueueFilter.answered,
            answeredIds: <String>{'a'}).map((q) => q.id),
        <String>['a'],
      );
      expect(
        filterModeratorQueue(approved, ModeratorQueueFilter.fresh,
            answeredIds: <String>{'a'}).map((q) => q.id),
        <String>['c'],
      );
    });

    test('rejected lists the rejected rows and excludes them from live buckets',
        () {
      final rejected = <ModeratorQuestion>[q('a')];
      expect(
        filterModeratorQueue(approved, ModeratorQueueFilter.rejected,
            rejected: rejected).map((q) => q.id),
        <String>['a'],
      );
      expect(
        filterModeratorQueue(approved, ModeratorQueueFilter.fresh,
            rejected: rejected).map((q) => q.id),
        <String>['c'],
      );
    });
  });
}
