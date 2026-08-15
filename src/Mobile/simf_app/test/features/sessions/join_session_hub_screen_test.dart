import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/sessions_repository.dart'
    show programmeSessionsProvider;
import 'package:simf_app/features/sessions/join_session_hub_screen.dart';

SessionListItem _item(String id, String title) => SessionListItem(
      id: id,
      code: 'C-$id',
      title: title,
      titleArabic: title,
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة',
      start: DateTime.utc(2026, 11, 23, 6),
      end: DateTime.utc(2026, 11, 23, 7),
      status: SessionStatus.scheduled,
      speakers: const <SessionSpeaker>[],
    );

Future<void> _pump(WidgetTester tester, List<SessionListItem> items) async {
  final router = GoRouter(
    initialLocation: '/sessions/join',
    routes: <RouteBase>[
      GoRoute(
        path: '/sessions/join',
        name: RouteNames.joinSessionHub,
        builder: (_, __) => const JoinSessionHubScreen(),
      ),
      GoRoute(
        path: '/sessions/:sessionId',
        name: RouteNames.sessionDetail,
        builder: (_, state) =>
            Scaffold(body: Text('DETAIL ${state.pathParameters['sessionId']}')),
      ),
    ],
  );
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        programmeSessionsProvider.overrideWith((ref) async => items),
      ],
      child: MaterialApp.router(
        routerConfig: router,
        locale: const Locale('en'),
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
  group('JoinSessionHubScreen (D-485)', () {
    testWidgets('lists the sessions; tapping one opens its detail',
        (tester) async {
      await _pump(tester,
          <SessionListItem>[_item('s1', 'Opening'), _item('s2', 'Closing')],);

      expect(find.text('Choose a session to join'), findsOneWidget);
      expect(find.text('Opening'), findsOneWidget);
      expect(find.text('Closing'), findsOneWidget);

      await tester.tap(find.text('Opening'));
      await tester.pumpAndSettle();
      expect(find.text('DETAIL s1'), findsOneWidget);
    });

    testWidgets('shows the empty state when there are no sessions',
        (tester) async {
      await _pump(tester, const <SessionListItem>[]);
      expect(find.text('No sessions'), findsOneWidget);
    });
  });
}
