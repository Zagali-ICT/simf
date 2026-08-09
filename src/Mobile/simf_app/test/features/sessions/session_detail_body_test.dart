import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/widgets/ask_host_card.dart';
import 'package:simf_app/features/sessions/widgets/session_detail_body.dart';
import 'package:simf_app/features/sessions/widgets/session_speaker_card.dart';

const _speaker = SessionSpeaker(
  id: 'sp1',
  name: 'Dr. Ali Al-Harbi',
  nameArabic: 'د. علي الحربي',
  displayOrder: 0,
  role: SessionSpeakerRole.speaker,
  title: 'Professor of Maritime Studies',
);

SessionDetail _detail({
  required DateTime start,
  required DateTime end,
  String? liveStreamUrl,
  SessionType? type,
  List<SessionSpeaker> speakers = const <SessionSpeaker>[],
}) =>
    SessionDetail(
      id: 's1',
      code: 'OP-1',
      title: 'Opening',
      titleArabic: 'الافتتاح',
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة الرئيسية',
      start: start,
      end: end,
      speakers: speakers,
      description: 'Welcome address',
      type: type,
      liveStreamUrl: liveStreamUrl,
    );

SessionSeatMap _seatMap() => const SessionSeatMap(
      rowLabels: <String>['A'],
      seatsPerRow: 1,
      reservedCells: <SeatCell>[],
      activeReservedCount: 0,
      hallCapacity: 1,
    );

Future<void> _pumpBody(
  WidgetTester tester, {
  required SessionDetail detail,
  required SessionSeatMap? seatMap,
  bool seatMapError = false,
  bool canAsk = true,
  VoidCallback? onRetrySeatMap,
  VoidCallback? onSessionLink,
  VoidCallback? onSessionSummary,
}) async {
  tester.view.physicalSize = const Size(1200, 2600);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(
    MaterialApp(
      supportedLocales: AppL10n.supportedLocales,
      localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
        ...AppL10n.localizationsDelegates,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      home: Scaffold(
        body: Builder(
          builder: (context) => SessionDetailBody(
            detail: detail,
            seatMap: seatMap,
            busy: false,
            l10n: AppL10n.of(context),
            baseUrl: 'http://test.local/api/v1',
            onAddToCalendar: () {},
            onRemind: () {},
            onSessionLink: onSessionLink ?? () {},
            onSessionSummary: onSessionSummary ?? () {},
            onAskHost: () {},
            onJoin: () {},
            canAsk: canAsk,
            seatMapError: seatMapError,
            onRetrySeatMap: onRetrySeatMap ?? () {},
            onCancelReservation: () {},
            onViewSeat: () {},
            onSpeaker: (_) {},
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  final now = saudiNow();

  group('SessionDetailBody ask card (S-4)', () {
    testWidgets('a FUTURE session shows the ask with the pre-session label',
        (tester) async {
      await _pumpBody(
        tester,
        detail: _detail(
          start: now.add(const Duration(hours: 1)),
          end: now.add(const Duration(hours: 2)),
        ),
        seatMap: _seatMap(),
      );

      expect(find.byType(AskHostCard), findsOneWidget);
      expect(find.text('Ask a question before it starts'), findsOneWidget);
    });

    testWidgets('an in-window in-person session (no live URL) shows the ask '
        'with the neutral live label', (tester) async {
      await _pumpBody(
        tester,
        detail: _detail(
          start: now.subtract(const Duration(hours: 1)),
          end: now.add(const Duration(hours: 1)),
        ),
        seatMap: _seatMap(),
      );

      expect(find.byType(AskHostCard), findsOneWidget);
      expect(find.text('Ask a question'), findsOneWidget);
      // Not the pre-session label once the session is live.
      expect(find.text('Ask a question before it starts'), findsNothing);
    });

    testWidgets('an in-window BROADCAST session (live URL) HIDES the ask on '
        'the detail (it lives on the live screen)', (tester) async {
      await _pumpBody(
        tester,
        detail: _detail(
          start: now.subtract(const Duration(hours: 1)),
          end: now.add(const Duration(hours: 1)),
          liveStreamUrl: 'https://live.example.sa/main.m3u8',
        ),
        seatMap: _seatMap(),
      );

      expect(find.byType(AskHostCard), findsNothing);
    });

    testWidgets('an ENDED session hides the ask (the window is closed)',
        (tester) async {
      await _pumpBody(
        tester,
        detail: _detail(
          start: now.subtract(const Duration(hours: 3)),
          end: now.subtract(const Duration(hours: 2)),
        ),
        seatMap: _seatMap(),
      );

      expect(find.byType(AskHostCard), findsNothing);
    });

    testWidgets('a guest / pending account (no seat map) sees the ask disabled',
        (tester) async {
      await _pumpBody(
        tester,
        detail: _detail(
          start: now.add(const Duration(hours: 1)),
          end: now.add(const Duration(hours: 2)),
        ),
        seatMap: null,
      );

      final card = find.byType(AskHostCard);
      expect(card, findsOneWidget);
      expect(tester.widget<AskHostCard>(card).enabled, isFalse);
    });

    // DEF-MOD-003 — the send-question route (#26) is attendee-only, so a
    // Staff / Moderator must not be offered a card that silently bounces them
    // Home. canAsk=false is what the screen passes for those roles.
    testWidgets('DEF-MOD-003: a role that cannot open send-question is not '
        'offered the ask card at all', (tester) async {
      await _pumpBody(
        tester,
        detail: _detail(
          start: now.add(const Duration(hours: 1)),
          end: now.add(const Duration(hours: 2)),
        ),
        seatMap: _seatMap(),
        canAsk: false,
      );

      expect(find.byType(AskHostCard), findsNothing);
    });
  });

  // #18 — an approved attendee whose seat map fails to load must get a retry,
  // not a silently-absent Join button.
  group('SessionDetailBody #18 seat-map load failure', () {
    testWidgets('a FAILED seat map (approved attendee) shows the error + retry '
        'instead of a Join button', (tester) async {
      var retried = false;
      await _pumpBody(
        tester,
        detail: _detail(
          start: now.add(const Duration(hours: 1)),
          end: now.add(const Duration(hours: 2)),
        ),
        seatMap: null,
        seatMapError: true,
        onRetrySeatMap: () => retried = true,
      );

      // No Join CTA (the seat map is null) …
      expect(
        find.widgetWithText(FilledButton, 'Join the session'),
        findsNothing,
      );
      // … the seat-map error + retry shows in its place.
      expect(
        find.text('Could not load the seat map.'),
        findsOneWidget,
      );
      final retry = find.widgetWithText(FilledButton, 'Retry');
      expect(retry, findsOneWidget);
      await tester.tap(retry);
      await tester.pump();
      expect(retried, isTrue);
    });

    testWidgets('an ENDED session with a failed seat map shows NO retry (it can '
        'no longer be joined)', (tester) async {
      await _pumpBody(
        tester,
        detail: _detail(
          start: now.subtract(const Duration(hours: 3)),
          end: now.subtract(const Duration(hours: 2)),
        ),
        seatMap: null,
        seatMapError: true,
      );

      expect(
        find.text('Could not load the seat map.'),
        findsNothing,
      );
    });
  });

  // Owner 2026-07-14 — the two header actions gate on the session's phase.
  group('SessionDetailBody header action gating', () {
    testWidgets('a FUTURE session shows the summary button INACTIVE (no tap)',
        (tester) async {
      var tapped = false;
      await _pumpBody(
        tester,
        detail: _detail(
          start: now.add(const Duration(hours: 1)),
          end: now.add(const Duration(hours: 2)),
        ),
        seatMap: _seatMap(),
        onSessionSummary: () => tapped = true,
      );

      await tester.tap(find.text('Session summary'));
      await tester.pump();
      expect(tapped, isFalse, reason: 'no summary exists for a future session');
    });

    testWidgets('an ENDED session shows the summary button ACTIVE',
        (tester) async {
      var tapped = false;
      await _pumpBody(
        tester,
        detail: _detail(
          start: now.subtract(const Duration(hours: 3)),
          end: now.subtract(const Duration(hours: 2)),
        ),
        seatMap: _seatMap(),
        onSessionSummary: () => tapped = true,
      );

      await tester.tap(find.text('Session summary'));
      await tester.pump();
      expect(tapped, isTrue);
    });

    testWidgets('a LIVE broadcast session shows the live button ACTIVE',
        (tester) async {
      var tapped = false;
      await _pumpBody(
        tester,
        detail: _detail(
          start: now.subtract(const Duration(hours: 1)),
          end: now.add(const Duration(hours: 1)),
          liveStreamUrl: 'https://live.example.sa/main.m3u8',
        ),
        seatMap: _seatMap(),
        onSessionLink: () => tapped = true,
      );

      await tester.tap(find.text('Session link'));
      await tester.pump();
      expect(tapped, isTrue);
    });

    testWidgets('a FUTURE session with a feed keeps the live button INACTIVE '
        '(the feed is not live yet)', (tester) async {
      var tapped = false;
      await _pumpBody(
        tester,
        detail: _detail(
          start: now.add(const Duration(hours: 1)),
          end: now.add(const Duration(hours: 2)),
          liveStreamUrl: 'https://live.example.sa/main.m3u8',
        ),
        seatMap: _seatMap(),
        onSessionLink: () => tapped = true,
      );

      await tester.tap(find.text('Session link'));
      await tester.pump();
      expect(tapped, isFalse);
    });
  });

  // #29 (owner Q10, 2026-07-30) — a WORKSHOP's detail is title + time ONLY.
  group('SessionDetailBody #29 workshop reduction', () {
    testWidgets('a WORKSHOP renders the title + time block and nothing else',
        (tester) async {
      await _pumpBody(
        tester,
        detail: _detail(
          start: now.add(const Duration(hours: 1)),
          end: now.add(const Duration(hours: 2)),
          type: SessionType.workshop,
          speakers: const <SessionSpeaker>[_speaker],
          liveStreamUrl: 'https://live.example.sa/main.m3u8',
        ),
        seatMap: _seatMap(),
      );

      // The title (and, with it, the header card carrying the time meta) stays.
      expect(find.text('Opening'), findsOneWidget);
      // Everything else is suppressed.
      expect(find.text('Description'), findsNothing);
      expect(find.text('Welcome address'), findsNothing);
      expect(find.text('Speakers'), findsNothing);
      expect(find.byType(SessionSpeakerCard), findsNothing);
      expect(find.byType(AskHostCard), findsNothing);
      expect(find.text('Session summary'), findsNothing);
      expect(find.text('Session link'), findsNothing);
      expect(
        find.widgetWithText(FilledButton, 'Join the session'),
        findsNothing,
      );
      expect(find.text('Add to calendar'), findsNothing);
      expect(find.text('Reminder'), findsNothing);
    });

    testWidgets('a non-workshop session keeps the full detail', (tester) async {
      await _pumpBody(
        tester,
        detail: _detail(
          start: now.add(const Duration(hours: 1)),
          end: now.add(const Duration(hours: 2)),
          type: SessionType.session,
          speakers: const <SessionSpeaker>[_speaker],
        ),
        seatMap: _seatMap(),
      );

      expect(find.text('Welcome address'), findsOneWidget);
      expect(find.byType(SessionSpeakerCard), findsOneWidget);
      expect(find.text('Session summary'), findsOneWidget);
    });

    testWidgets('an UNTYPED session (older API → null type) keeps the full '
        'detail', (tester) async {
      await _pumpBody(
        tester,
        detail: _detail(
          start: now.add(const Duration(hours: 1)),
          end: now.add(const Duration(hours: 2)),
          speakers: const <SessionSpeaker>[_speaker],
        ),
        seatMap: _seatMap(),
      );

      expect(find.text('Welcome address'), findsOneWidget);
      expect(find.byType(SessionSpeakerCard), findsOneWidget);
      expect(find.text('Session summary'), findsOneWidget);
    });
  });
}
