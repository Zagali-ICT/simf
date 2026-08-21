import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/data/session_speaker.dart';
import 'package:simf_app/features/sessions/widgets/ask_host_card.dart';
import 'package:simf_app/features/sessions/widgets/session_booking_actions.dart';
import 'package:simf_app/features/sessions/widgets/session_detail_body.dart';

/// Pins: `SessionDetailBody` phases on the SAUDI wall clock, not the device's.
///
/// Re-runs itself in a child `flutter test` under `TZ=UTC` — on a +03:00
/// machine `saudiNow()` and `DateTime.now()` are the same instant, so no
/// in-process assertion can discriminate.

/// Set on the child process; its presence is what selects the real tests.
const String _childMarker = 'SIMF_SESSION_DETAIL_CLOCK_CHILD';

const String _selfPath =
    'test/features/sessions/widgets/session_detail_body_clock_test.dart';

SessionDetail _detail({
  required DateTime start,
  required DateTime end,
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
      start: start,
      end: end,
      speakers: const <SessionSpeaker>[],
      description: 'Welcome address',
      liveStreamUrl: liveStreamUrl,
    );

const _seatMap = SessionSeatMap(
  rowLabels: <String>['A'],
  seatsPerRow: 1,
  reservedCells: <SeatCell>[],
  activeReservedCount: 0,
  hallCapacity: 1,
);

Future<void> _pumpBody(WidgetTester tester, SessionDetail detail) async {
  tester.view.physicalSize = const Size(1200, 2600);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(
    MaterialApp(
      locale: const Locale('en'),
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
            seatMap: _seatMap,
            busy: false,
            l10n: AppL10n.of(context),
            baseUrl: 'http://test.local/api/v1',
            onAddToCalendar: () {},
            onRemind: () {},
            onSessionLink: () {},
            onSessionSummary: () {},
            onAskHost: () {},
            onJoin: () {},
            canAsk: true,
            seatMapError: false,
            onRetrySeatMap: () {},
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
  if (Platform.environment[_childMarker] == '1') {
    _saudiClockTests();
    return;
  }
  _reRunWithADeviceClockThatIsNotRiyadh();
}

/// The child: device clock = UTC, event clock = +03:00. Every session below is
/// timed so the two clocks disagree about its phase.
void _saudiClockTests() {
  test('the child really is running on a non-Riyadh device clock', () {
    expect(
      DateTime.now().timeZoneOffset,
      isNot(const Duration(hours: 3)),
      reason: 'The device clock must differ from Riyadh for these tests to '
          'discriminate at all.',
    );
  });

  group('a session that has ENDED on the Saudi clock', () {
    // Ended 10 minutes ago in Riyadh; on the UTC device clock it has not
    // started yet.
    SessionDetail ended() => _detail(
          start: saudiNow().subtract(const Duration(hours: 2)),
          end: saudiNow().subtract(const Duration(minutes: 10)),
        );

    testWidgets('drops the Join CTA', (tester) async {
      await _pumpBody(tester, ended());

      expect(
        find.byType(SessionJoinButton),
        findsNothing,
        reason: 'The session is over in Riyadh, so there is nothing to join. '
            'Offering the CTA means the page phased on the DEVICE clock, which '
            'still thinks the session is an hour away.',
      );
    });

    testWidgets('drops the ask-the-host card', (tester) async {
      await _pumpBody(tester, ended());

      expect(
        find.byType(AskHostCard),
        findsNothing,
        reason: 'The backend closes questions at End. An ask offered here is '
            'a tap the server will refuse.',
      );
    });
  });

  group('a session that is LIVE on the Saudi clock', () {
    testWidgets('offers the in-hall ask, not the ahead-of-time one',
        (tester) async {
      // Running in Riyadh; the UTC device clock reads it as hours away.
      await _pumpBody(
        tester,
        _detail(
          start: saudiNow().subtract(const Duration(minutes: 30)),
          end: saudiNow().add(const Duration(hours: 1)),
        ),
      );

      expect(find.text('Ask a question'), findsOneWidget);
      expect(
        find.text('Ask a question before it starts'),
        findsNothing,
        reason: 'The session is already running in Riyadh. The pre-session '
            'wording means the label was chosen against the device clock.',
      );
    });
  });
}

/// The parent: re-runs this file with the device clock moved off +03:00.
void _reRunWithADeviceClockThatIsNotRiyadh() {
  test(
    'the session detail phases on the Saudi clock, not the device clock',
    () async {
      final result = await Process.run(
        'flutter',
        <String>['test', '--no-pub', _selfPath],
        environment: <String, String>{'TZ': 'UTC', _childMarker: '1'},
        runInShell: true,
      );

      final output = '${result.stdout}\n${result.stderr}';
      expect(
        result.exitCode,
        0,
        reason: 'The detail gated on the device clock rather than the Saudi '
            'one. Child run output:\n$output',
      );
      expect(
        output,
        contains('All tests passed!'),
        reason: 'The child run produced no passing tests, so nothing was '
            'actually asserted. Child run output:\n$output',
      );
    },
    timeout: const Timeout(Duration(minutes: 10)),
  );
}
