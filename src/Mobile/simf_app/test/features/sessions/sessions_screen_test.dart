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

// The day banner builds {base}/app/assets/ProgrammeDayImage/{id}/image; the test
// network-image loads fail, so the banner shows its fallback.
const _testConfig = SimfDataConfig(
  baseUrl: 'http://test.local/api/v1',
  appKey: 'test',
  deviceType: SimfDeviceType.android,
);

SessionListItem _session(
  String id,
  int hour,
  String title, {
  SessionType? type,
}) {
  final start = DateTime.utc(2026, 9, 13, hour);
  return SessionListItem(
    id: id,
    code: id.toUpperCase(),
    title: title,
    titleArabic: '',
    hallId: 'h1',
    hallName: 'Hall A',
    hallNameArabic: 'القاعة أ',
    startUtc: start,
    endUtc: start.add(const Duration(hours: 1)),
    status: SessionStatus.scheduled,
    speakers: const <SessionSpeaker>[],
    description: 'Session abstract',
    type: type,
  );
}

ProgrammeDay _day(
  String id,
  DateTime date,
  String title,
  String titleArabic,
  List<SessionListItem> sessions, {
  bool hasImage = false,
}) {
  return ProgrammeDay(
    id: id,
    date: date,
    title: title,
    titleArabic: titleArabic,
    displayOrder: 0,
    hasImage: hasImage,
    sessions: sessions,
  );
}

class _FakeSessionsRepository implements SessionsRepository {
  _FakeSessionsRepository({
    this.days = const <ProgrammeDay>[],
    this.fail = false,
  });

  final List<ProgrammeDay> days;
  final bool fail;
  int calls = 0;

  @override
  Future<List<SessionListItem>> getSessions() async => const <SessionListItem>[];

  @override
  Future<List<ProgrammeDay>> getDays() async {
    calls++;
    if (fail) {
      throw const ApiFailure(code: ApiErrorCodes.clientNetwork, message: 'x');
    }
    return days;
  }
}

Future<void> _pump(
  WidgetTester tester, {
  required SessionsRepository repo,
  Locale locale = const Locale('en'),
}) async {
  // A tall surface renders the whole list (day strip + banner + tabs + rows)
  // without lazy-ListView fold-off, so finds + taps reach every row.
  tester.view.physicalSize = const Size(412, 2400);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.reset);
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
        simfDataConfigProvider.overrideWithValue(_testConfig),
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

ProgrammeDay _dayOne(List<SessionListItem> sessions) =>
    _day('d1', DateTime(2026, 9, 13), 'Day One', 'اليوم الأول', sessions);

void main() {
  group('SessionsScreen (Page 016 — KSA frame 883:2308)', () {
    testWidgets('renders the header, day title banner, type tabs and session '
        'rows', (tester) async {
      await _pump(
        tester,
        repo: _FakeSessionsRepository(days: <ProgrammeDay>[
          _dayOne(<SessionListItem>[
            _session('s1', 9, 'Opening'),
            _session('s2', 11, 'Panel'),
          ],),
        ],),
      );

      expect(find.text('Forum programme'), findsOneWidget); // screen header
      expect(find.text('Agenda'), findsWidgets); // active bottom-nav label
      expect(find.text('Day One'), findsOneWidget); // day title (تفاصيل اليوم)
      expect(find.text('Schedule'), findsOneWidget); // المواعيد
      expect(find.text('All'), findsOneWidget); // type tabs
      expect(find.text('Workshops'), findsOneWidget);
      expect(find.text('Events'), findsOneWidget);
      expect(find.textContaining('Opening'), findsOneWidget);
      expect(find.textContaining('Panel'), findsOneWidget);
      // The updated rows lead with a gold calendar glyph (no numbered prefix /
      // trailing chevron) and carry a start/end time rail.
      expect(find.byIcon(Icons.calendar_today_outlined), findsWidgets);
    });

    testWidgets('the search box filters the list', (tester) async {
      await _pump(
        tester,
        repo: _FakeSessionsRepository(days: <ProgrammeDay>[
          _dayOne(<SessionListItem>[
            _session('s1', 9, 'Opening keynote'),
            _session('s2', 11, 'Security panel'),
          ],),
        ],),
      );

      await tester.enterText(find.byType(TextField), 'keynote');
      await tester.pumpAndSettle();

      expect(find.textContaining('Opening keynote'), findsOneWidget);
      expect(find.textContaining('Security panel'), findsNothing);
    });

    testWidgets('the type tabs filter the day by session type (883:2320)',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeSessionsRepository(days: <ProgrammeDay>[
          _dayOne(<SessionListItem>[
            _session('w1', 9, 'Diving workshop', type: SessionType.workshop),
            _session('e1', 11, 'Gala dinner', type: SessionType.event),
          ],),
        ],),
      );

      // الكل (All) shows both.
      expect(find.textContaining('Diving workshop'), findsOneWidget);
      expect(find.textContaining('Gala dinner'), findsOneWidget);

      // Workshops → only the workshop.
      await tester.tap(find.text('Workshops'));
      await tester.pumpAndSettle();
      expect(find.textContaining('Diving workshop'), findsOneWidget);
      expect(find.textContaining('Gala dinner'), findsNothing);

      // Events → only the event.
      await tester.tap(find.text('Events'));
      await tester.pumpAndSettle();
      expect(find.textContaining('Diving workshop'), findsNothing);
      expect(find.textContaining('Gala dinner'), findsOneWidget);
    });

    testWidgets('the day strip switches the selected day', (tester) async {
      await _pump(
        tester,
        repo: _FakeSessionsRepository(days: <ProgrammeDay>[
          _day('d1', DateTime(2026, 9, 13), 'Day One', 'اليوم الأول',
              <SessionListItem>[_session('s1', 9, 'Opening')],),
          _day('d2', DateTime(2026, 9, 14), 'Day Two', 'اليوم الثاني',
              <SessionListItem>[_session('s2', 9, 'Closing')],),
        ],),
      );

      // Day 1 selected by default → its title + session.
      expect(find.text('Day One'), findsOneWidget);
      expect(find.textContaining('Opening'), findsOneWidget);
      expect(find.textContaining('Closing'), findsNothing);

      // Tap day 2's cell (the "14").
      await tester.tap(find.text('14'));
      await tester.pumpAndSettle();
      expect(find.text('Day Two'), findsOneWidget);
      expect(find.textContaining('Closing'), findsOneWidget);
      expect(find.textContaining('Opening'), findsNothing);
    });

    testWidgets('the selected day cell inverts to navy', (tester) async {
      await _pump(
        tester,
        repo: _FakeSessionsRepository(days: <ProgrammeDay>[
          _dayOne(<SessionListItem>[_session('s1', 9, 'Opening')],),
        ],),
      );

      final cell = tester.widget<Container>(
        find
            .ancestor(of: find.text('13'), matching: find.byType(Container))
            .first,
      );
      expect((cell.decoration! as BoxDecoration).color, SimfTokens.navy);
    });

    testWidgets('tapping a row navigates to the session detail',
        (tester) async {
      await _pump(
        tester,
        repo: _FakeSessionsRepository(days: <ProgrammeDay>[
          _dayOne(<SessionListItem>[_session('fut', 9, 'Closing keynote')],),
        ],),
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
        repo: _FakeSessionsRepository(days: <ProgrammeDay>[
          _dayOne(<SessionListItem>[_session('s1', 9, 'Opening')],),
        ],),
        locale: const Locale('ar'),
      );

      expect(find.text('برنامج الملتقى'), findsOneWidget); // screen header
      expect(find.text('الأجندة'), findsWidgets); // active bottom-nav label
      expect(find.text('اليوم الأول'), findsOneWidget); // day title
      expect(find.text('المواعيد'), findsOneWidget);
      expect(find.text('الكل'), findsOneWidget); // All tab
      expect(
        Directionality.of(tester.element(find.text('المواعيد'))),
        TextDirection.rtl,
      );
    });
  });
}
