import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/sessions/data/presentation_models.dart';
import 'package:simf_app/features/sessions/data/presentation_repository.dart';
import 'package:simf_app/features/sessions/session_presentations_screen.dart';

PresentationItem _item(String id, String title) => PresentationItem(
      id: id,
      sessionId: 's-$id',
      sessionTitle: title,
      sessionTitleArabic: title,
      sessionStartUtc: DateTime.utc(2026, 11, 23, 6),
      speakerName: 'Dr. Omari',
      speakerNameArabic: 'د. العمري',
      fileName: '$id.pdf',
      contentType: 'application/pdf',
      sizeBytes: 2048,
    );

/// A router with the screen plus stub detail/summary targets so we can assert
/// where each affordance navigates (owner 2026-07-03).
GoRouter _router() => GoRouter(
      initialLocation: '/p',
      routes: <RouteBase>[
        GoRoute(
          path: '/p',
          name: RouteNames.sessionPresentations,
          builder: (_, __) => const SessionPresentationsScreen(),
        ),
        GoRoute(
          path: '/sessions/:sessionId',
          name: RouteNames.sessionDetail,
          builder: (_, state) => Scaffold(
            body: Text('DETAIL ${state.pathParameters['sessionId']}'),
          ),
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

Future<void> _pump(
  WidgetTester tester,
  List<PresentationItem> items, {
  GoRouter? router,
}) async {
  final config = router ?? _router();
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        presentationsProvider
            .overrideWith((ref) async => PresentationsPage(items)),
      ],
      child: MaterialApp.router(
        routerConfig: config,
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
  group('SessionPresentationsScreen (Figma 1388:7621)', () {
    testWidgets('lists the sessions with the speaker and a Download button',
        (tester) async {
      await _pump(tester, <PresentationItem>[_item('p1', 'Future of Investment')]);

      expect(find.text('Future of Investment'), findsOneWidget);
      expect(find.text('Dr. Omari'), findsOneWidget);
      expect(find.text('Download'), findsOneWidget);
      // The الكل / All day tab is present.
      expect(find.text('All'), findsOneWidget);
      // "Day 1" appears as both the day tab and the card's event-day label
      // (Figma 1388:7664) — the card label is the one this re-skin added.
      expect(find.text('Day 1'), findsNWidgets(2));
    });

    testWidgets('shows the empty state when there are no presentations',
        (tester) async {
      await _pump(tester, const <PresentationItem>[]);
      expect(find.text('No presentations available yet.'), findsOneWidget);
    });

    testWidgets('tapping the card opens that session detail (17)',
        (tester) async {
      await _pump(tester, <PresentationItem>[_item('p1', 'Future of Investment')]);

      await tester.tap(find.text('Future of Investment'));
      await tester.pumpAndSettle();

      expect(find.text('DETAIL s-p1'), findsOneWidget);
    });

    testWidgets('tapping تحميل opens that session summary (34)',
        (tester) async {
      await _pump(tester, <PresentationItem>[_item('p1', 'Future of Investment')]);

      await tester.tap(find.text('Download'));
      await tester.pumpAndSettle();

      expect(find.text('SUMMARY s-p1'), findsOneWidget);
    });
  });
}
