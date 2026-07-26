import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/moderation/data/moderation_models.dart';
import 'package:simf_app/features/moderation/data/moderation_repository.dart';
import 'package:simf_app/features/moderation/session_moderate_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

ModeratorQuestion _q(
  String id, {
  bool pushed = false,
  String name = 'Raed',
  ModeratorQuestionStatus status = ModeratorQuestionStatus.approved,
}) =>
    ModeratorQuestion.fromJson(<String, dynamic>{
      'id': id,
      'sessionId': 's1',
      'submittedByDisplayName': name,
      'questionText': 'Q $id text',
      'isPushed': pushed,
      'status': ModeratorQuestionStatus.values.indexOf(status),
      'createdAt': '2026-01-01T10:00:00Z',
    });

/// A fake that behaves like the real backend: the moderation writes change the
/// PERSISTED status, so a reload of the screen re-reads the same buckets.
class _FakeRepo implements ModerationRepository {
  _FakeRepo({List<ModeratorQuestion> queue = const <ModeratorQuestion>[], this.status = 0})
      : _rows = <ModeratorQuestion>[...queue];

  final List<ModeratorQuestion> _rows;
  final int status; // 0 = ok; otherwise the ApiFailure httpStatus
  int pushCalls = 0;
  int hideCalls = 0;
  int answeredCalls = 0;
  bool failWrites = false;

  @override
  Future<List<ModeratorQuestion>> getQueue(
    String sessionId, {
    ModeratorQuestionStatus? status,
  }) async {
    if (this.status != 0) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: this.status,
      );
    }
    if (status == null) {
      // The working desk: approved + answered.
      return _rows.where((r) => !r.isRejected).toList(growable: false);
    }
    return _rows.where((r) => r.status == status).toList(growable: false);
  }

  @override
  Future<ModeratorQuestion> push(String sessionId, String questionId) async {
    pushCalls++;
    _failIfAsked();
    return _q(questionId, pushed: true);
  }

  @override
  Future<ModeratorQuestion> setHidden(
    String sessionId,
    String questionId, {
    required bool isHidden,
  }) async {
    hideCalls++;
    _failIfAsked();
    return _write(
      questionId,
      isHidden
          ? ModeratorQuestionStatus.hidden
          : ModeratorQuestionStatus.approved,
    );
  }

  @override
  Future<ModeratorQuestion> setAnswered(
    String sessionId,
    String questionId, {
    required bool isAnswered,
  }) async {
    answeredCalls++;
    _failIfAsked();
    return _write(
      questionId,
      isAnswered
          ? ModeratorQuestionStatus.answered
          : ModeratorQuestionStatus.approved,
    );
  }

  ModeratorQuestion _write(String questionId, ModeratorQuestionStatus next) {
    final index = _rows.indexWhere((r) => r.id == questionId);
    final updated = _rows[index].withStatus(next);
    _rows[index] = updated;
    return updated;
  }

  void _failIfAsked() {
    if (failWrites) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: 500,
      );
    }
  }
}

Future<void> _pump(
  WidgetTester tester,
  _FakeRepo repo, {
  Locale locale = const Locale('en'),
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        moderationRepositoryProvider.overrideWithValue(repo),
      ],
      child: MaterialApp(
        locale: locale,
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: const SessionModerateScreen(sessionId: 's1'),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('SessionModerateScreen (D-405, frame 758:5307)', () {
    testWidgets('renders the queue, the role pill and the five chips',
        (tester) async {
      await _pump(
        tester,
        _FakeRepo(queue: <ModeratorQuestion>[_q('a'), _q('b', pushed: true)]),
      );

      expect(find.text('Session questions'), findsOneWidget);
      expect(find.text('Moderator'), findsOneWidget); // محاوِر pill
      expect(find.text('Q a text'), findsOneWidget);
      expect(find.text('Q b text'), findsOneWidget);
      // The five filter chips (Figma 1461:12227).
      expect(find.text('All'), findsOneWidget);
      expect(find.text('New'), findsWidgets);
      expect(find.text('Accepted'), findsOneWidget);
      expect(find.text('Rejected'), findsWidgets);
    });

    testWidgets('a 403 shows the not-a-moderator state', (tester) async {
      await _pump(tester, _FakeRepo(status: 403));
      expect(
        find.textContaining('not a moderator for this session'),
        findsOneWidget,
      );
    });

    testWidgets('a non-403 failure shows the retry surface', (tester) async {
      final repo = _FakeRepo(status: 500);
      await _pump(tester, repo);
      expect(find.widgetWithText(FilledButton, 'Retry'), findsOneWidget);
    });

    testWidgets('empty queue shows the empty state', (tester) async {
      await _pump(tester, _FakeRepo());
      expect(find.textContaining('No approved questions'), findsOneWidget);
    });

    testWidgets('the chip filters the queue', (tester) async {
      await _pump(
        tester,
        _FakeRepo(queue: <ModeratorQuestion>[_q('a'), _q('b', pushed: true)]),
      );
      // Tap the "New" chip → only the fresh (not pushed) question shows.
      await tester.tap(find.text('New').first);
      await tester.pumpAndSettle();
      expect(find.text('Q a text'), findsOneWidget);
      expect(find.text('Q b text'), findsNothing);
    });

    testWidgets('on-stage calls push', (tester) async {
      final repo = _FakeRepo(queue: <ModeratorQuestion>[_q('a')]);
      await _pump(tester, repo);

      await tester.tap(find.byIcon(Icons.access_time));
      await tester.pumpAndSettle();
      expect(repo.pushCalls, 1);
    });

    testWidgets('reject calls hide and moves the row to the Rejected tab',
        (tester) async {
      final repo = _FakeRepo(queue: <ModeratorQuestion>[_q('a')]);
      await _pump(tester, repo);

      await tester.tap(find.byIcon(Icons.close));
      await tester.pumpAndSettle();
      expect(repo.hideCalls, 1);

      // The rejected row lists under the Rejected chip …
      await tester.tap(find.text('Rejected').first);
      await tester.pumpAndSettle();
      expect(find.text('Q a text'), findsOneWidget);

      // …and is gone from the New tab.
      await tester.tap(find.text('New').first);
      await tester.pumpAndSettle();
      expect(find.text('Q a text'), findsNothing);
    });

    // DEF-MOD-001 — the answered mark is a REAL backend call and survives a
    // full reload of the screen (the old build kept it in a local Set that died
    // on screen exit, so a co-moderator or a restart lost it).
    testWidgets('DEF-MOD-001: answered calls the endpoint and the mark survives '
        'a reload of the screen', (tester) async {
      final repo = _FakeRepo(queue: <ModeratorQuestion>[_q('a')]);
      await _pump(tester, repo);

      await tester.tap(find.byIcon(Icons.check));
      await tester.pumpAndSettle();
      expect(repo.answeredCalls, 1);

      await tester.tap(find.text('Answered').first);
      await tester.pumpAndSettle();
      expect(find.text('Q a text'), findsOneWidget);

      // Rebuild the screen from scratch against the SAME backing store — the
      // mark is still there because it is persisted, not screen state.
      await _pump(tester, repo);
      await tester.tap(find.text('Answered').first);
      await tester.pumpAndSettle();
      expect(find.text('Q a text'), findsOneWidget);
      await tester.tap(find.text('New').first);
      await tester.pumpAndSettle();
      expect(find.text('Q a text'), findsNothing);
    });

    // DEF-MOD-002 — a rejected question is fetched back from the server
    // (?status=Hidden), so a mis-click is recoverable after leaving the screen.
    testWidgets('DEF-MOD-002: a rejected question survives a reload and can be '
        'restored', (tester) async {
      final repo = _FakeRepo(
        queue: <ModeratorQuestion>[
          _q('a', status: ModeratorQuestionStatus.hidden),
        ],
      );
      await _pump(tester, repo);

      // A freshly opened desk already lists the previously rejected row.
      await tester.tap(find.text('Rejected').first);
      await tester.pumpAndSettle();
      expect(find.text('Q a text'), findsOneWidget);

      // Restoring it (via the answered action, which un-hides first) puts it
      // back on the working desk.
      await tester.tap(find.byIcon(Icons.check));
      await tester.pumpAndSettle();
      expect(repo.hideCalls, 1);
      expect(repo.answeredCalls, 1);

      await tester.tap(find.text('Answered').first);
      await tester.pumpAndSettle();
      expect(find.text('Q a text'), findsOneWidget);
    });

    // DEF-MOD-001 — a failed write must not leave the optimistic mark on screen.
    testWidgets('DEF-MOD-001: a failed answered call rolls the row back',
        (tester) async {
      final repo = _FakeRepo(queue: <ModeratorQuestion>[_q('a')])
        ..failWrites = true;
      await _pump(tester, repo);

      await tester.tap(find.byIcon(Icons.check));
      await tester.pumpAndSettle();
      expect(repo.answeredCalls, 1);
      expect(find.text('Action failed. Try again.'), findsOneWidget);

      // Still in New, not in Answered.
      await tester.tap(find.text('Answered').first);
      await tester.pumpAndSettle();
      expect(find.text('Q a text'), findsNothing);
      await tester.tap(find.text('New').first);
      await tester.pumpAndSettle();
      expect(find.text('Q a text'), findsOneWidget);
    });
  });
}
