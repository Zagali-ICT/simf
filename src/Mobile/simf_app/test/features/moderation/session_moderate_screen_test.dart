import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/moderation/data/moderation_models.dart';
import 'package:simf_app/features/moderation/data/moderation_repository.dart';
import 'package:simf_app/features/moderation/session_moderate_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

ModeratorQuestion _q(String id, {bool pushed = false, String name = 'Raed'}) =>
    ModeratorQuestion.fromJson(<String, dynamic>{
      'id': id,
      'sessionId': 's1',
      'submittedByDisplayName': name,
      'questionText': 'Q $id text',
      'isPushed': pushed,
      'createdAt': '2026-01-01T10:00:00Z',
    });

class _FakeRepo implements ModerationRepository {
  _FakeRepo({this.queue = const <ModeratorQuestion>[], this.status = 0});

  List<ModeratorQuestion> queue;
  final int status; // 0 = ok; otherwise the ApiFailure httpStatus
  int pushCalls = 0;
  int hideCalls = 0;

  @override
  Future<List<ModeratorQuestion>> getQueue(String sessionId) async {
    if (status != 0) {
      throw ApiFailure(
        code: ApiErrorCodes.clientNetwork,
        message: 'x',
        httpStatus: status,
      );
    }
    return queue;
  }

  @override
  Future<ModeratorQuestion> push(String sessionId, String questionId) async {
    pushCalls++;
    return _q(questionId, pushed: true);
  }

  @override
  Future<ModeratorQuestion> setHidden(
    String sessionId,
    String questionId, {
    required bool isHidden,
  }) async {
    hideCalls++;
    return _q(questionId);
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

      // The rejected row still lists under the Rejected chip (session-local).
      await tester.tap(find.text('Rejected').first);
      await tester.pumpAndSettle();
      expect(find.text('Q a text'), findsOneWidget);

      // ...and is gone from the New tab.
      await tester.tap(find.text('New').first);
      await tester.pumpAndSettle();
      expect(find.text('Q a text'), findsNothing);
    });

    testWidgets('answered marks the row (client-only) into the Answered tab',
        (tester) async {
      final repo = _FakeRepo(queue: <ModeratorQuestion>[_q('a')]);
      await _pump(tester, repo);

      await tester.tap(find.byIcon(Icons.check));
      await tester.pumpAndSettle();
      // No backend call for "answered" (no such status).
      expect(repo.hideCalls, 0);
      expect(repo.pushCalls, 0);

      await tester.tap(find.text('Answered').first);
      await tester.pumpAndSettle();
      expect(find.text('Q a text'), findsOneWidget);
    });
  });
}
