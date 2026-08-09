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
      // Saudi wall-clock carries no zone, so a decoded value must NOT be
      // left untagged: tagging it would let a later toLocal() shift it by the
      // device offset (owner decision 2026-07-31).
      expect(q.createdAt.isUtc, isFalse);
    });

    test('defaults missing fields safely (speaker, not pushed)', () {
      final q = ModeratorQuestion.fromJson(const <String, dynamic>{});
      expect(q.recipient, QuestionRecipient.speaker);
      expect(q.isPushed, isFalse);
      expect(q.isOnStage, isFalse);
      // A row with no `status` on the wire is the desk's approved bucket.
      expect(q.status, ModeratorQuestionStatus.approved);
      expect(q.isAnswered, isFalse);
      expect(q.isRejected, isFalse);
    });

    // DEF-MOD-001 / DEF-MOD-002 — the desk state is PERSISTED, so the wire
    // carries it: Pending=0, Approved=1, Hidden=2, Answered=3.
    test('DEF-MOD-001: status maps the persisted QuestionStatus', () {
      ModeratorQuestion withStatus(int s) =>
          ModeratorQuestion.fromJson(<String, dynamic>{'id': 'q', 'status': s});

      expect(withStatus(0).status, ModeratorQuestionStatus.pending);
      expect(withStatus(1).status, ModeratorQuestionStatus.approved);
      expect(withStatus(2).status, ModeratorQuestionStatus.hidden);
      expect(withStatus(2).isRejected, isTrue);
      expect(withStatus(3).status, ModeratorQuestionStatus.answered);
      expect(withStatus(3).isAnswered, isTrue);
    });

    test('DEF-MOD-001: an answered question is no longer on stage', () {
      final q = ModeratorQuestion.fromJson(<String, dynamic>{
        'id': 'q',
        'isPushed': true,
        'status': 3,
      });
      expect(q.isPushed, isTrue);
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
    ModeratorQuestion q(String id, {bool pushed = false, int status = 1}) =>
        ModeratorQuestion.fromJson(<String, dynamic>{
          'id': id,
          'isPushed': pushed,
          'status': status,
        });
    // The working desk as the server returns it: Approved + Answered.
    final desk = <ModeratorQuestion>[
      q('a'),
      q('b', pushed: true),
      q('c'),
    ];

    test('all returns the desk plus the rejected rows', () {
      expect(filterModeratorQueue(desk, ModeratorQueueFilter.all), hasLength(3));
      expect(
        filterModeratorQueue(desk, ModeratorQueueFilter.all,
            rejected: <ModeratorQuestion>[q('r', status: 2)],).map((q) => q.id),
        <String>['a', 'b', 'c', 'r'],
      );
    });

    test('fresh = approved, not on stage, not answered', () {
      final fresh = filterModeratorQueue(desk, ModeratorQueueFilter.fresh);
      expect(fresh.map((q) => q.id), <String>['a', 'c']);
    });

    test('accepted = on stage (pushed)', () {
      final accepted =
          filterModeratorQueue(desk, ModeratorQueueFilter.accepted);
      expect(accepted.map((q) => q.id), <String>['b']);
    });

    // DEF-MOD-001 — answered is read from the PERSISTED status, not a
    // session-local set, so the bucket survives a reload of the screen.
    test('DEF-MOD-001: answered comes from the wire status and drops from fresh',
        () {
      final withAnswered = <ModeratorQuestion>[
        q('a', status: 3),
        q('b', pushed: true),
        q('c'),
      ];
      expect(
        filterModeratorQueue(withAnswered, ModeratorQueueFilter.answered)
            .map((q) => q.id),
        <String>['a'],
      );
      expect(
        filterModeratorQueue(withAnswered, ModeratorQueueFilter.fresh)
            .map((q) => q.id),
        <String>['c'],
      );
    });

    // DEF-MOD-002 — the rejected bucket is its own server read
    // (?status=Hidden);
    // it is never mixed into the working desk.
    test('DEF-MOD-002: rejected lists the separately fetched hidden rows', () {
      final rejected = <ModeratorQuestion>[q('r', status: 2)];
      expect(
        filterModeratorQueue(desk, ModeratorQueueFilter.rejected,
            rejected: rejected,).map((q) => q.id),
        <String>['r'],
      );
      expect(
        filterModeratorQueue(desk, ModeratorQueueFilter.fresh,
            rejected: rejected,).map((q) => q.id),
        <String>['a', 'c'],
      );
    });
  });
}
