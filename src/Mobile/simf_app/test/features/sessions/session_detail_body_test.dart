import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/widgets/ask_host_card.dart';
import 'package:simf_app/features/sessions/widgets/session_detail_body.dart';

SessionDetail _detail({
  required DateTime startUtc,
  required DateTime endUtc,
  String? liveStreamUrl,
}) =>
    SessionDetail(
      id: 's1',
      code: 'OP-1',
      title: 'Opening',
      titleArabic: 'الافتتاح',
      hallId: 'h1',
      hallName: 'Main Hall',
      hallNameArabic: 'القاعة الرئيسية',
      startUtc: startUtc,
      endUtc: endUtc,
      speakers: const <SessionSpeaker>[],
      description: 'Welcome address',
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
            onSessionLink: () {},
            onSessionSummary: () {},
            onAskHost: () {},
            onJoin: () {},
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
  final now = DateTime.now().toUtc();

  group('SessionDetailBody ask card (S-4)', () {
    testWidgets('a FUTURE session shows the ask with the pre-session label',
        (tester) async {
      await _pumpBody(
        tester,
        detail: _detail(
          startUtc: now.add(const Duration(hours: 1)),
          endUtc: now.add(const Duration(hours: 2)),
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
          startUtc: now.subtract(const Duration(hours: 1)),
          endUtc: now.add(const Duration(hours: 1)),
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
          startUtc: now.subtract(const Duration(hours: 1)),
          endUtc: now.add(const Duration(hours: 1)),
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
          startUtc: now.subtract(const Duration(hours: 3)),
          endUtc: now.subtract(const Duration(hours: 2)),
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
          startUtc: now.add(const Duration(hours: 1)),
          endUtc: now.add(const Duration(hours: 2)),
        ),
        seatMap: null,
      );

      final card = find.byType(AskHostCard);
      expect(card, findsOneWidget);
      expect(tester.widget<AskHostCard>(card).enabled, isFalse);
    });
  });
}
