import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sessions/data/presentation_models.dart';
import 'package:simf_app/features/sessions/data/presentation_repository.dart';
import 'package:simf_app/features/sessions/data/presentation_summary_gate.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/sessions_repository.dart';
import 'package:simf_app/features/sessions/session_presentations_screen.dart';

PresentationItem _item(String id, String title) => PresentationItem(
      id: id,
      sessionId: 's-$id',
      sessionTitle: title,
      sessionTitleArabic: title,
      sessionStart: DateTime.utc(2026, 11, 23, 6),
      speakerName: 'Dr. Omari',
      speakerNameArabic: 'د. العمري',
      fileName: '$id.pdf',
      contentType: 'application/pdf',
      sizeBytes: 2048,
    );

/// The programme session behind a presentation row (matched on [id] =
/// presentation.sessionId). [hasSummary] drives the تحميل gate.
SessionListItem _session(String id, {required bool hasSummary}) =>
    SessionListItem(
      id: id,
      code: 'C-$id',
      title: 't',
      titleArabic: 't',
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة',
      start: DateTime.utc(2026, 11, 23, 6),
      end: DateTime.utc(2026, 11, 23, 7),
      status: SessionStatus.scheduled,
      speakers: const <SessionSpeaker>[],
      hasPublishedSummary: hasSummary,
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
  List<SessionListItem> sessions = const <SessionListItem>[],
  GoRouter? router,
}) async {
  final config = router ?? _router();
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        presentationsProvider
            .overrideWith((ref) async => PresentationsPage(items)),
        programmeSessionsProvider.overrideWith((ref) async => sessions),
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
    testWidgets('lists the sessions with the speaker + a session-summary button',
        (tester) async {
      await _pump(tester, <PresentationItem>[_item('p1', 'Future of Investment')]);

      expect(find.text('Future of Investment'), findsOneWidget);
      expect(find.text('Dr. Omari'), findsOneWidget);
      expect(find.text('Session summary'), findsOneWidget);
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

    testWidgets(
        'a published-summary session → ملخص active, opens the summary (34)',
        (tester) async {
      await _pump(
        tester,
        <PresentationItem>[_item('p1', 'Future of Investment')],
        sessions: <SessionListItem>[_session('s-p1', hasSummary: true)],
      );

      // Active = white label on the gold fill (owner 2026-07-14).
      final label = tester.widget<Text>(find.text('Session summary'));
      expect(label.style?.color, Colors.white);

      await tester.tap(find.text('Session summary'));
      await tester.pumpAndSettle();

      expect(find.text('SUMMARY s-p1'), findsOneWidget);
    });

    testWidgets(
        'no published summary yet → ملخص greyed + inactive (no navigation)',
        (tester) async {
      // Owner 2026-07-14 bug #1: a future/unsummarised session must NOT offer an
      // active summary button.
      await _pump(
        tester,
        <PresentationItem>[_item('p1', 'Future of Investment')],
        sessions: <SessionListItem>[_session('s-p1', hasSummary: false)],
      );

      // Inactive = the shell's disabled-label colour.
      final label = tester.widget<Text>(find.text('Session summary'));
      expect(label.style?.color, SimfTokens.navyDisabledText);

      // The tap is consumed and dropped (warnIfMissed off — the disabled button
      // swallows the pointer): no summary, and it doesn't fall through to detail.
      await tester.tap(find.text('Session summary'), warnIfMissed: false);
      await tester.pumpAndSettle();

      expect(find.text('SUMMARY s-p1'), findsNothing);
      expect(find.text('Future of Investment'), findsOneWidget);
    });
  });

  group('presentationSummaryReady', () {
    final future = _item('p1', 't'); // starts 2026-11-23 (after "now")
    final past = PresentationItem(
      id: 'p2',
      sessionId: 's-p2',
      sessionTitle: 't',
      sessionTitleArabic: 't',
      sessionStart: DateTime.utc(2020),
      speakerName: 's',
      speakerNameArabic: 's',
      fileName: 'f.pdf',
      contentType: 'application/pdf',
      sizeBytes: 1,
    );
    final now = DateTime.utc(2026, 7, 14);

    test('matched session with a published summary → active', () {
      expect(
        presentationSummaryReady(future, _session('s-p1', hasSummary: true), now),
        isTrue,
      );
    });

    test('matched session without a published summary → inactive', () {
      expect(
        presentationSummaryReady(future, _session('s-p1', hasSummary: false), now),
        isFalse,
      );
    });

    test('programme not loaded + future start → inactive (fallback)', () {
      expect(presentationSummaryReady(future, null, now), isFalse);
    });

    test('programme not loaded + past start → active (graceful fallback)', () {
      expect(presentationSummaryReady(past, null, now), isTrue);
    });
  });
}
