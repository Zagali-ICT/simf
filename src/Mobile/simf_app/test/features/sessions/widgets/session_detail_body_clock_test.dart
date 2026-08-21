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

/// The session detail must phase off the SAUDI wall clock, never the device's.
///
/// `SessionDetailBody` asks `detail.phase(saudiNow())` — and, for the ask card,
/// `detail.start.isAfter(saudiNow())`. Swap any of the three for
/// `DateTime.now()` and the page gates on wherever the phone happens to be: a
/// traveller (or anyone whose phone auto-set its zone abroad) is offered the
/// Join CTA on a session that finished hours ago, or shown the "before it
/// starts" ask on one that is already running.
///
/// **Why this test spawns a second `flutter test` instead of just asserting.**
/// `saudiNow()` is `DateTime.now()` minus the device offset plus +03:00, so on
/// a +03:00 machine — which is every SIMF machine, and the CI agent — the two
/// return the identical instant. The substitution is a literal no-op here and
/// NO in-process test can see it; a test that computes its expectation from the
/// ambient clock certifies nothing. The device zone is fixed when the VM
/// starts, so the only way to make the two disagree is another process. This
/// file therefore re-runs ITSELF under `TZ=UTC`, where the device clock sits
/// three hours behind Riyadh, and the sessions below are timed to fall on
/// opposite sides of that gap. The child spawn costs about five seconds
/// (measured 2026-08-21), so this stays an ordinary member of the suite.
///
/// The alternative considered and rejected: grepping the source for
/// `DateTime.now()`. A grep pins one spelling of the defect, and this branch
/// has already had a sweep walk straight past a source check by writing the
/// same bug differently.

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
/// placed so that the Saudi reading and the device reading fall in DIFFERENT
/// phases, which is the whole point — read on the device clock these cases
/// answer the opposite way, and that is what makes the assertion able to fail.
void _saudiClockTests() {
  test('the child really is running on a non-Riyadh device clock', () {
    // Belt and braces: if the TZ override ever stops taking effect, this fails
    // loudly rather than letting the three tests below pass vacuously.
    expect(
      DateTime.now().timeZoneOffset,
      isNot(const Duration(hours: 3)),
      reason: 'The device clock must differ from Riyadh for these tests to '
          'discriminate at all.',
    );
  });

  group('a session that has ENDED on the Saudi clock', () {
    // Ended 10 minutes ago in Riyadh; on the UTC device clock it has not even
    // started (it reads as beginning in an hour).
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
      // Started half an hour ago in Riyadh; the UTC device clock reads it as
      // starting in two and a half hours.
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
