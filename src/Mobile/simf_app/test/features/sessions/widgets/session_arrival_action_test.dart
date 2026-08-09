import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/sessions/data/hall_attendance_repository.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/widgets/session_arrival_action.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// `gate-check-in-status` — owner 2026-07-31: a session arrival is established
/// by the GATE SCAN at the hall door, so the session detail only REPORTS the
/// `HallAttendance` row the door opened. These cover the three render states and
/// the one distinction that keeps being lost elsewhere: a failed read is not the
/// same thing as "not checked in yet".
const String _sessionId = 's-1';

/// Answers `getStatus` from a scripted list — one entry per call, the last entry
/// repeating — so the retry path can fail first and succeed second. A
/// [HallAttendanceStatus] entry is returned; an [ApiFailure] entry is thrown.
class _FakeAttendance implements HallAttendanceRepository {
  _FakeAttendance.answering(HallAttendanceStatus status)
      : _answers = <Object>[status];

  _FakeAttendance.failing(ApiFailure failure) : _answers = <Object>[failure];

  _FakeAttendance.failingThenAnswering(
    ApiFailure failure,
    HallAttendanceStatus status,
  ) : _answers = <Object>[failure, status];

  final List<Object> _answers;

  int statusCalls = 0;

  @override
  Future<HallAttendanceStatus> getStatus(String sessionId) async {
    final index =
        statusCalls < _answers.length ? statusCalls : _answers.length - 1;
    statusCalls++;
    final answer = _answers[index];
    if (answer is ApiFailure) {
      throw answer;
    }
    return answer as HallAttendanceStatus;
  }

  // The card is READ-ONLY. The device no longer claims an arrival (that was the
  // removed GPS self check-in), so either write reaching the repository is the
  // regression these throws catch.
  @override
  Future<HallAttendanceStatus> recordArrival(
    String sessionId, {
    required double lat,
    required double lon,
  }) async =>
      throw StateError('the check-in card must never post an arrival');

  @override
  Future<HallAttendanceStatus> recordDeparture(String sessionId) async =>
      throw StateError('the check-in card must never post a departure');
}

Future<void> _pumpCard(
  WidgetTester tester, {
  required _FakeAttendance repository,
  Locale locale = const Locale('en'),
  bool hasEnded = false,
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        hallAttendanceRepositoryProvider.overrideWithValue(repository),
      ],
      child: MaterialApp(
        locale: locale,
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: Builder(
          builder: (context) => Scaffold(
            body: SessionArrivalAction(
              sessionId: _sessionId,
              hasEnded: hasEnded,
              l10n: AppL10n.of(context),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

/// A transport failure — no envelope, so no `httpStatus`: the exact shape the
/// api client raises when the server cannot be reached.
const ApiFailure _readFailed = ApiFailure(
  code: ApiErrorCodes.clientNetwork,
  message: 'Could not reach the server.',
);

void main() {
  group('SessionArrivalAction — hall check-in status (gate scan, FR-305/506)',
      () {
    testWidgets('a door scan renders the recorded arrival on the Saudi clock',
        (tester) async {
      final repository = _FakeAttendance.answering(
        HallAttendanceStatus(
          arrived: true,
          enter: DateTime(2026, 11, 23, 10, 30),
          method: HallAttendanceMethod.qrScan,
        ),
      );
      await _pumpCard(tester, repository: repository);

      expect(
        find.text('Your hall arrival is recorded · 10:30 AM'),
        findsOneWidget,
      );
      // Nothing to act on — the door records the arrival, not the phone.
      expect(find.byType(FilledButton), findsNothing);
      expect(repository.statusCalls, 1);
    });

    testWidgets('a closed row shows the departure under the arrival',
        (tester) async {
      await _pumpCard(
        tester,
        repository: _FakeAttendance.answering(
          HallAttendanceStatus(
            arrived: false,
            enter: DateTime(2026, 11, 23, 10, 30),
            leave: DateTime(2026, 11, 23, 11, 45),
            method: HallAttendanceMethod.qrScan,
          ),
        ),
      );

      expect(
        find.text('Your hall arrival is recorded · 10:30 AM'),
        findsOneWidget,
      );
      expect(
        find.text('Your departure is recorded · 11:45 AM'),
        findsOneWidget,
      );
      // A recorded-then-closed attendance is still an attendance: it must not
      // fall back to the "check in at the door" instruction.
      expect(
        find.textContaining('Show your badge at the hall door'),
        findsNothing,
      );
    });

    testWidgets('no attendance row reads as a calm instruction, not an error',
        (tester) async {
      await _pumpCard(
        tester,
        repository: _FakeAttendance.answering(
          const HallAttendanceStatus(arrived: false),
        ),
      );

      expect(
        find.text(
          'You are not checked in yet. Show your badge at the hall door to be '
          'checked in.',
        ),
        findsOneWidget,
      );
      // Not an error: no retry affordance, and no claimed arrival.
      expect(find.text('Retry'), findsNothing);
      expect(
        find.textContaining('Your hall arrival is recorded'),
        findsNothing,
      );
    });

    testWidgets('an ENDED session with no attendance says nothing at all',
        (tester) async {
      // An attendee attends a handful of sessions out of dozens. Rendering
      // "show your badge at the hall door" on every past session they simply
      // did not go to is an instruction they cannot act on, so the strip stays
      // silent once the session is over and no scan was recorded.
      await _pumpCard(
        tester,
        hasEnded: true,
        repository: _FakeAttendance.answering(
          const HallAttendanceStatus(arrived: false),
        ),
      );

      expect(
        find.textContaining('Show your badge at the hall door'),
        findsNothing,
      );
      expect(find.text('Retry'), findsNothing);
    });

    testWidgets('an ENDED session STILL shows an arrival that was recorded',
        (tester) async {
      // The silence above is only for the empty case — a scan that did happen
      // stays visible for good, because that is the attendance record itself.
      await _pumpCard(
        tester,
        hasEnded: true,
        repository: _FakeAttendance.answering(
          HallAttendanceStatus(
            arrived: true,
            enter: DateTime(2026, 11, 23, 10, 30),
          ),
        ),
      );

      expect(
        find.textContaining('Your hall arrival is recorded'),
        findsOneWidget,
      );
    });

    testWidgets('a FAILED read is never collapsed into "not checked in"',
        (tester) async {
      await _pumpCard(
        tester,
        repository: _FakeAttendance.failing(_readFailed),
      );

      expect(
        find.text('Could not load your hall check-in status.'),
        findsOneWidget,
      );
      expect(find.text('Retry'), findsOneWidget);
      // The two states mean different things — only one of them is fixed by
      // trying again, so the failure must not borrow the "not yet" wording.
      expect(
        find.textContaining('Show your badge at the hall door'),
        findsNothing,
      );
    });

    testWidgets('retry re-reads the status and renders the recovered state',
        (tester) async {
      final repository = _FakeAttendance.failingThenAnswering(
        _readFailed,
        HallAttendanceStatus(
          arrived: true,
          enter: DateTime(2026, 11, 23, 10, 30),
          method: HallAttendanceMethod.qrScan,
        ),
      );
      await _pumpCard(tester, repository: repository);

      expect(find.text('Retry'), findsOneWidget);
      await tester.tap(find.text('Retry'));
      await tester.pumpAndSettle();

      expect(repository.statusCalls, 2);
      expect(
        find.text('Your hall arrival is recorded · 10:30 AM'),
        findsOneWidget,
      );
      expect(find.text('Retry'), findsNothing);
    });

    testWidgets('the Arabic card renders the Arabic wording', (tester) async {
      await _pumpCard(
        tester,
        repository: _FakeAttendance.answering(
          const HallAttendanceStatus(arrived: false),
        ),
        locale: const Locale('ar'),
      );

      expect(
        find.textContaining('أبرز بطاقتك عند باب القاعة'),
        findsOneWidget,
      );
    });
  });

  group('HallAttendanceStatus wire decoding', () {
    test('decodes the arrival envelope, method int and all', () {
      final status = HallAttendanceStatus.fromJson(<String, dynamic>{
        'arrived': true,
        'enter': '2026-11-23T07:30:00Z',
        'leave': null,
        'method': 1,
      });

      expect(status.arrived, isTrue);
      expect(status.enter, DateTime(2026, 11, 23, 10, 30));
      expect(status.leave, isNull);
      expect(status.method, HallAttendanceMethod.geofence);
    });

    test('accepts the method by name as well as by int', () {
      expect(
        HallAttendanceStatus.fromJson(<String, dynamic>{'method': 'QrScan'})
            .method,
        HallAttendanceMethod.qrScan,
      );
    });

    test('an absent / unknown method degrades to null rather than throwing',
        () {
      expect(
        HallAttendanceStatus.fromJson(const <String, dynamic>{}).method,
        isNull,
      );
      expect(
        HallAttendanceStatus.fromJson(<String, dynamic>{'method': 99}).method,
        isNull,
      );
    });
  });
}
