// FR-MOD-001 — a moderator could not DISCOVER which sessions they moderate.
// Home offered only "all sessions", session detail showed the Q&A desk action
// on every one of them, and a missing per-session grant surfaced as a 403 after
// the tap. The moderator's operational home now lists جلساتي — the sessions
// GET /app/sessions/moderated says they actually hold a grant on — each tapping
// straight through to its desk.
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/home/widgets/operational_homes.dart';
import 'package:simf_app/features/moderation/data/moderation_models.dart';
import 'package:simf_app/features/moderation/data/moderation_repository.dart';
import 'package:simf_app/features/moderation/widgets/moderated_session_tile.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';

ModeratedSession _session(String id, {String title = 'Opening Panel'}) =>
    ModeratedSession(
      sessionId: id,
      title: title,
      titleArabic: 'الجلسة الافتتاحية',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة الرئيسية',
      // 09:00 Saudi (+3) — the tile renders the local wall clock, never a zoned
      // stamp.
      start: DateTime(2026, 3, 1, 9),
      end: DateTime(2026, 3, 1, 10),
    );

/// Renders `ModeratorHome` with the grant list [sessions] already resolved, or
/// failing when [fails] is set. Returns the route the last tap navigated to.
Future<List<String>> _pump(
  WidgetTester tester, {
  List<ModeratedSession> sessions = const <ModeratedSession>[],
  bool fails = false,
  Locale locale = const Locale('en'),
}) async {
  final pushed = <String>[];
  final router = GoRouter(
    initialLocation: '/',
    routes: <RouteBase>[
      GoRoute(
        path: '/',
        builder: (context, _) => ModeratorHome(l10n: AppL10n.of(context)),
      ),
      GoRoute(
        path: '/sessions',
        name: RouteNames.sessions,
        builder: (_, __) {
          pushed.add('sessions');
          return const Scaffold(body: Text('SESSIONS'));
        },
      ),
      GoRoute(
        path: '/sessions/:sessionId/moderate',
        name: RouteNames.sessionModerate,
        builder: (_, state) {
          pushed.add('moderate:${state.pathParameters['sessionId']}');
          return const Scaffold(body: Text('DESK'));
        },
      ),
    ],
  );

  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        myModeratedSessionsProvider.overrideWith((ref) async {
          if (fails) {
            throw const ApiFailure(
              code: ApiErrorCodes.clientNetwork,
              message: 'x',
              httpStatus: 500,
            );
          }
          return sessions;
        }),
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
  return pushed;
}

void main() {
  group('ModeratorHome (FR-MOD-001 — جلساتي)', () {
    testWidgets('lists the sessions the moderator holds a grant on',
        (tester) async {
      await _pump(
        tester,
        sessions: <ModeratedSession>[
          _session('s1'),
          _session('s2', title: 'Closing Panel'),
        ],
      );

      expect(find.text('My sessions'), findsOneWidget);
      expect(find.byType(ModeratedSessionTile), findsNWidgets(2));
      expect(find.text('Opening Panel'), findsOneWidget);
      expect(find.text('Closing Panel'), findsOneWidget);
      // Hall + Saudi wall clock (09:00, +3 from the 06:00Z above), 12-hour and
      // never a absolute instants.
      expect(find.textContaining('Main Hall'), findsWidgets);
      expect(find.textContaining('9:00 AM'), findsWidgets);
      // The programme entry stays — a moderator still browses every session.
      expect(find.text('Sessions'), findsOneWidget);
    });

    testWidgets('a granted session taps straight through to its Q&A desk',
        (tester) async {
      final pushed = await _pump(
        tester,
        sessions: <ModeratedSession>[_session('s7')],
      );

      await tester.tap(find.byType(ModeratedSessionTile));
      await tester.pumpAndSettle();

      expect(pushed, contains('moderate:s7'));
    });

    // A moderator with no grants must be TOLD so, not shown a bare programme
    // link that leaves them guessing which session is theirs.
    testWidgets('no grants shows the empty note, not a session row',
        (tester) async {
      await _pump(tester);

      expect(find.text('My sessions'), findsOneWidget);
      expect(
        find.text('You are not assigned to any session yet.'),
        findsOneWidget,
      );
      expect(find.byType(ModeratedSessionTile), findsNothing);
    });

    testWidgets('a failed load shows the shared error surface with retry',
        (tester) async {
      await _pump(tester, fails: true);

      expect(find.byType(SimfErrorState), findsOneWidget);
      expect(
        find.text('Could not load your sessions. Try again.'),
        findsOneWidget,
      );
    });

    testWidgets('RTL renders the Arabic heading and title', (tester) async {
      await _pump(
        tester,
        sessions: <ModeratedSession>[_session('s1')],
        locale: const Locale('ar'),
      );

      expect(find.text('جلساتي'), findsOneWidget);
      expect(find.text('الجلسة الافتتاحية'), findsOneWidget);
      expect(
        Directionality.of(tester.element(find.byType(ModeratedSessionTile))),
        TextDirection.rtl,
      );
    });
  });
}
