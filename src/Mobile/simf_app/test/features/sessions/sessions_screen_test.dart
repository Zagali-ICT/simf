import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/sessions_repository.dart';
import 'package:simf_app/features/sessions/sessions_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

SessionListItem _session({
  required String id,
  required DateTime startUtc,
  required String title,
}) {
  return SessionListItem(
    id: id,
    code: id.toUpperCase(),
    title: title,
    titleArabic: '',
    hallId: 'h1',
    hallName: 'Hall A',
    hallNameArabic: 'القاعة أ',
    startUtc: startUtc,
    endUtc: startUtc.add(const Duration(hours: 1)),
    status: SessionStatus.scheduled,
    speakers: const <SessionSpeaker>[],
  );
}

// Far-future / far-past so the "Upcoming" (startUtc >= now) filter is
// deterministic regardless of when the test runs.
final _future = _session(
  id: 'fut',
  startUtc: DateTime.utc(2099, 11, 25, 9),
  title: 'Closing keynote',
);
final _future2 = _session(
  id: 'fut2',
  startUtc: DateTime.utc(2099, 11, 25, 11),
  title: 'Maritime security panel',
);
final _past = _session(
  id: 'old',
  startUtc: DateTime.utc(2000, 1, 1, 9),
  title: 'Archived opening',
);

class _FakeSessionsRepository implements SessionsRepository {
  _FakeSessionsRepository({this.sessions = const <SessionListItem>[], this.fail = false});

  final List<SessionListItem> sessions;
  final bool fail;
  int calls = 0;

  @override
  Future<List<SessionListItem>> getSessions() async {
    calls++;
    if (fail) {
      throw const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'x');
    }
    return sessions;
  }
}

Future<void> _pump(
  WidgetTester tester, {
  required SessionsRepository repo,
  Locale locale = const Locale('en'),
}) async {
  final router = GoRouter(
    initialLocation: '/',
    routes: <RouteBase>[
      GoRoute(
        path: '/',
        name: RouteNames.sessions,
        builder: (_, __) => const SessionsScreen(),
      ),
      GoRoute(
        path: '/sessions/:sessionId',
        name: RouteNames.sessionDetail,
        builder: (_, state) => Scaffold(
          body: Text('DETAIL ${state.pathParameters['sessionId']}'),
        ),
      ),
    ],
  );

  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        sessionsRepositoryProvider.overrideWithValue(repo),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: locale,
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('SessionsScreen (Page 016)', () {
    testWidgets('renders a row per session with an index chip', (tester) async {
      await _pump(
        tester,
        repo: _FakeSessionsRepository(
          sessions: <SessionListItem>[_future, _future2],
        ),
      );

      expect(find.text('Closing keynote'), findsOneWidget);
      expect(find.text('Maritime security panel'), findsOneWidget);
      expect(find.text('01'), findsOneWidget);
      expect(find.text('02'), findsOneWidget);
    });

    testWidgets('the search box filters the list', (tester) async {
      await _pump(
        tester,
        repo: _FakeSessionsRepository(
          sessions: <SessionListItem>[_future, _future2],
        ),
      );

      await tester.enterText(find.byType(TextField), 'keynote');
      await tester.pumpAndSettle();

      expect(find.text('Closing keynote'), findsOneWidget);
      expect(find.text('Maritime security panel'), findsNothing);
    });

    testWidgets('the Forum pill reveals past sessions hidden by Upcoming',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeSessionsRepository(
          sessions: <SessionListItem>[_past, _future],
        ),
      );

      // Default view is Upcoming → the past session is hidden.
      expect(find.text('Archived opening'), findsNothing);
      expect(find.text('Closing keynote'), findsOneWidget);

      await tester.tap(find.text('Forum'));
      await tester.pumpAndSettle();

      expect(find.text('Archived opening'), findsOneWidget);
    });

    testWidgets('tapping a row navigates to the session detail', (tester) async {
      await _pump(
        tester,
        repo: _FakeSessionsRepository(sessions: <SessionListItem>[_future]),
      );

      await tester.tap(find.text('Closing keynote'));
      await tester.pumpAndSettle();

      expect(find.text('DETAIL fut'), findsOneWidget);
    });

    testWidgets('an empty programme shows the empty state', (tester) async {
      await _pump(tester, repo: _FakeSessionsRepository());
      expect(find.text('No sessions'), findsOneWidget);
    });

    testWidgets('a load failure shows the error + retry, which re-fetches',
        (tester) async {
      final repo = _FakeSessionsRepository(fail: true);
      await _pump(tester, repo: repo);

      expect(find.text('Could not load the sessions.'), findsOneWidget);
      final retry = find.widgetWithText(FilledButton, 'Retry');
      expect(retry, findsOneWidget);

      await tester.tap(retry);
      await tester.pumpAndSettle();
      expect(repo.calls, greaterThanOrEqualTo(2));
    });
  });
}
