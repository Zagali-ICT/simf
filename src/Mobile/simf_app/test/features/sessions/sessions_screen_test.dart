import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
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
  startUtc: DateTime.utc(2099, 11, 26, 11),
  title: 'Maritime security panel',
);
final _past = _session(
  id: 'old',
  startUtc: DateTime.utc(2000, 1, 1, 9),
  title: 'Archived opening',
);

class _FakeSessionsRepository implements SessionsRepository {
  _FakeSessionsRepository({
    this.sessions = const <SessionListItem>[],
    this.fail = false,
  });

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
    initialLocation: '/sessions',
    routes: <RouteBase>[
      GoRoute(
        path: '/sessions',
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
      for (final (name, path, label) in <(String, String, String)>[
        (RouteNames.home, '/', 'HOME'),
        (RouteNames.badge, '/badge', 'BADGE'),
        (RouteNames.venueMap, '/map', 'MAP'),
        (RouteNames.myArea, '/my-area', 'MY-AREA'),
      ])
        GoRoute(
          name: name,
          path: path,
          builder: (c, s) => Scaffold(body: Text(label)),
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
  group('SessionsScreen (Page 016 — KSA frame 215:767)', () {
    testWidgets('renders the agenda chrome and a numbered row per session',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeSessionsRepository(
          sessions: <SessionListItem>[_future, _future2],
        ),
      );

      expect(find.text('Agenda'), findsWidgets); // header + active nav label
      expect(find.text('Schedule'), findsOneWidget);
      expect(find.text('Event agenda'), findsOneWidget);
      expect(find.text('Upcoming agenda'), findsOneWidget);
      expect(find.textContaining('Closing keynote'), findsOneWidget);
      expect(find.textContaining('Maritime security panel'), findsOneWidget);
      // The gold row indices (the trailing space keeps the matcher off the
      // zero-padded time chips, e.g. "02:00").
      expect(find.textContaining('01 '), findsOneWidget);
      expect(find.textContaining('02 '), findsOneWidget);
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

      expect(find.textContaining('Closing keynote'), findsOneWidget);
      expect(find.textContaining('Maritime security panel'), findsNothing);
    });

    testWidgets('the Event-agenda pill reveals past sessions hidden by '
        'Upcoming', (tester) async {
      await _pump(
        tester,
        repo: _FakeSessionsRepository(
          sessions: <SessionListItem>[_past, _future],
        ),
      );

      // Default view is Upcoming → the past session is hidden.
      expect(find.textContaining('Archived opening'), findsNothing);
      expect(find.textContaining('Closing keynote'), findsOneWidget);

      await tester.tap(find.text('Event agenda'));
      await tester.pumpAndSettle();

      expect(find.textContaining('Archived opening'), findsOneWidget);
    });

    testWidgets('the day strip filters to one day and re-tap clears it',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeSessionsRepository(
          sessions: <SessionListItem>[_future, _future2],
        ),
      );

      // Two distinct programme days → two day cells.
      final dayOne = find.text(_future.startLocal.day.toString());
      expect(dayOne, findsOneWidget);

      await tester.tap(dayOne);
      await tester.pumpAndSettle();
      expect(find.textContaining('Closing keynote'), findsOneWidget);
      expect(find.textContaining('Maritime security panel'), findsNothing);

      // Re-tap clears back to all days (no "all days" pill in the frame).
      await tester.tap(dayOne);
      await tester.pumpAndSettle();
      expect(find.textContaining('Maritime security panel'), findsOneWidget);
    });

    testWidgets('the selected day cell inverts to navy', (tester) async {
      await _pump(
        tester,
        repo: _FakeSessionsRepository(sessions: <SessionListItem>[_future]),
      );

      final dayText = _future.startLocal.day.toString();
      await tester.tap(find.text(dayText));
      await tester.pumpAndSettle();

      final cell = tester.widget<Container>(
        find
            .ancestor(of: find.text(dayText), matching: find.byType(Container))
            .first,
      );
      expect((cell.decoration! as BoxDecoration).color, SimfTokens.navy);
    });

    testWidgets('tapping a row navigates to the session detail',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeSessionsRepository(sessions: <SessionListItem>[_future]),
      );

      await tester.tap(find.textContaining('Closing keynote'));
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

    testWidgets('renders right-to-left in Arabic', (tester) async {
      await _pump(
        tester,
        repo: _FakeSessionsRepository(sessions: <SessionListItem>[_future]),
        locale: const Locale('ar'),
      );

      expect(find.text('الأجندة'), findsWidgets);
      expect(find.text('المواعيد'), findsOneWidget);
      expect(
        Directionality.of(tester.element(find.text('المواعيد'))),
        TextDirection.rtl,
      );
    });
  });
}
