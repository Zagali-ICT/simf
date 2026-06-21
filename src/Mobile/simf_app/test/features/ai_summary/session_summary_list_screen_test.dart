import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/ai_summary/session_summary_list_screen.dart';
import 'package:simf_app/features/ai_summary/session_summary_screen.dart'
    show aiSummarySessionsProvider;
import 'package:simf_app/features/sessions/data/session_models.dart';

SessionListItem _item(String id, String title) => SessionListItem(
      id: id,
      code: 'C-$id',
      title: title,
      titleArabic: title,
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة',
      startUtc: DateTime.utc(2026, 11, 23, 6),
      endUtc: DateTime.utc(2026, 11, 23, 7),
      status: SessionStatus.scheduled,
      speakers: const <SessionSpeaker>[],
    );

Future<void> _pump(WidgetTester tester, List<SessionListItem> items) async {
  final router = GoRouter(
    initialLocation: '/session-summaries',
    routes: <RouteBase>[
      GoRoute(
        path: '/session-summaries',
        name: RouteNames.sessionSummaryList,
        builder: (_, __) => const SessionSummaryListScreen(),
      ),
      GoRoute(
        path: '/ai-summary',
        name: RouteNames.aiSummary,
        builder: (_, state) => Scaffold(
          body: Text('SUMMARY ${state.uri.queryParameters['sessionId']}'),
        ),
      ),
    ],
  );
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        aiSummarySessionsProvider.overrideWith((ref) async => items),
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
  group('SessionSummaryListScreen (#1/#6)', () {
    testWidgets('lists the sessions; tapping one opens its summary details',
        (tester) async {
      await _pump(
        tester,
        <SessionListItem>[_item('s1', 'Opening'), _item('s2', 'Closing')],
      );

      expect(find.text('Opening'), findsOneWidget);
      expect(find.text('Closing'), findsOneWidget);

      await tester.tap(find.text('Opening'));
      await tester.pumpAndSettle();
      // Navigates to the AI-summary details for that session (sessionId in query).
      expect(find.text('SUMMARY s1'), findsOneWidget);
    });

    testWidgets('shows the empty state when there are no sessions',
        (tester) async {
      await _pump(tester, const <SessionListItem>[]);
      expect(find.text('No sessions available yet.'), findsOneWidget);
    });
  });
}
