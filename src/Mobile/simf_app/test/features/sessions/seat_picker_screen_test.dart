import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/seat_map_repository.dart';
import 'package:simf_app/features/sessions/seat_picker_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';

SessionSeatMap _map() => const SessionSeatMap(
      rowLabels: <String>['A', 'B'],
      seatsPerRow: 3,
      reservedCells: <SeatCell>[
        SeatCell(
            rowLabel: 'A',
            seatNumber: 1,
            kind: SeatReservationKind.userBooking,),
      ],
      activeReservedCount: 1,
      hallCapacity: 6,
      sessionTitle: 'Opening',
    );

// A ragged layout: row A has 4 seats, row B has 10 — used to prove the picker
// draws no phantom seats on the short row.
SessionSeatMap _variableMap() => const SessionSeatMap(
      rowLabels: <String>['A', 'B'],
      seatsPerRow: 10, // legacy fallback = max(counts)
      seatCounts: <int>[4, 10],
      reservedCells: <SeatCell>[],
      activeReservedCount: 0,
      hallCapacity: 14,
      sessionTitle: 'Ragged',
    );

// B1 — the same map, but the caller already holds B1: the picker opens in
// CHANGE mode (title/hint/CTA switch, no auto-pick, confirm names both seats).
SessionSeatMap _heldMap() => const SessionSeatMap(
      rowLabels: <String>['A', 'B'],
      seatsPerRow: 3,
      reservedCells: <SeatCell>[
        SeatCell(
            rowLabel: 'A',
            seatNumber: 1,
            kind: SeatReservationKind.userBooking,),
        SeatCell(
            rowLabel: 'B',
            seatNumber: 1,
            kind: SeatReservationKind.userBooking,),
      ],
      myCell: SeatCell(
        rowLabel: 'B',
        seatNumber: 1,
        kind: SeatReservationKind.userBooking,
      ),
      activeReservedCount: 2,
      hallCapacity: 6,
      sessionTitle: 'Opening',
    );

class _FakePickerRepo implements SeatMapRepository {
  _FakePickerRepo(
    this.map, {
    this.failReserve = false,
    this.randomSessionFull = false,
    this.moveFailure,
  });

  final SessionSeatMap map;
  final bool failReserve;
  // When set, reserveRandom throws SEAT_SESSION_FULL (the capacity cap) so the
  // picker's "no places remain" branch can be tested.
  final bool randomSessionFull;
  // B1 — when set, moveSeat throws it (the lost-race / rule-refusal branches).
  final ApiFailure? moveFailure;
  String? reservedRow;
  int? reservedSeat;
  int randomCalls = 0;
  String? movedRow;
  int? movedSeat;

  @override
  Future<SessionSeatMap> getSeatMap(String sessionId) async => map;

  @override
  Future<MyReservation> reserveSeat(
    String sessionId, {
    required String rowLabel,
    required int seatNumber,
  }) async {
    if (failReserve) {
      throw const ApiFailure(
        code: 'SEAT_ALREADY_RESERVED',
        message: 'taken',
        httpStatus: 409,
      );
    }
    reservedRow = rowLabel;
    reservedSeat = seatNumber;
    return MyReservation(
      reservationId: 'r1',
      sessionId: sessionId,
      rowLabel: rowLabel,
      seatNumber: seatNumber,
      kind: SeatReservationKind.userBooking,
      status: BookingStatus.pending,
    );
  }

  @override
  Future<MyReservation> reserveRandom(String sessionId) async {
    randomCalls++;
    if (randomSessionFull) {
      throw const ApiFailure(
        code: 'SEAT_SESSION_FULL',
        message: 'full',
        httpStatus: 409,
      );
    }
    return const MyReservation(
      reservationId: 'r2',
      sessionId: 's1',
      rowLabel: 'A',
      seatNumber: 2,
      kind: SeatReservationKind.randomAssignment,
      status: BookingStatus.pending,
    );
  }

  @override
  Future<MyReservation> joinOpenSeating(String sessionId) =>
      throw UnimplementedError();

  @override
  Future<void> releaseMine(String sessionId) => throw UnimplementedError();

  @override
  Future<MyReservation> moveSeat(
    String sessionId, {
    required String rowLabel,
    required int seatNumber,
  }) async {
    final failure = moveFailure;
    if (failure != null) {
      throw failure;
    }
    movedRow = rowLabel;
    movedSeat = seatNumber;
    return MyReservation(
      reservationId: 'r3',
      sessionId: sessionId,
      rowLabel: rowLabel,
      seatNumber: seatNumber,
      kind: SeatReservationKind.userBooking,
      status: BookingStatus.approved,
    );
  }
}

Future<_FakePickerRepo> _pump(WidgetTester tester,
    {_FakePickerRepo? repo,}) async {
  tester.view.physicalSize = const Size(1000, 2200);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  final pickerRepo = repo ?? _FakePickerRepo(_map());
  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        seatMapRepositoryProvider.overrideWithValue(pickerRepo),
      ],
      child: MaterialApp(
        locale: const Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        // Push the picker so its pop-with-true on success returns here.
        home: Builder(
          builder: (context) => Scaffold(
            body: Center(
              child: ElevatedButton(
                onPressed: () => Navigator.of(context).push(
                  MaterialPageRoute<bool>(
                    builder: (_) => const SeatPickerScreen(sessionId: 's1'),
                  ),
                ),
                child: const Text('OPEN'),
              ),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  await tester.tap(find.text('OPEN'));
  await tester.pumpAndSettle();
  return pickerRepo;
}

void main() {
  group('SeatPickerScreen (D-485)', () {
    testWidgets('renders the grid + hint, and the auto-pick CTA',
        (tester) async {
      await _pump(tester);
      expect(find.text('Opening'), findsOneWidget);
      expect(find.text('Tap an available seat to reserve it'), findsOneWidget);
      expect(
        find.widgetWithText(FilledButton, 'Auto-pick a seat'),
        findsOneWidget,
      );
    });

    testWidgets(
        'tapping a seat SELECTS it (chip appears, no reserve); the '
        'Confirm CTA commits it, shows the D-750 alert, then pops on OK',
        (tester) async {
      final repo = await _pump(tester);
      // A2 is available (only A1 is reserved). Tapping SELECTS — it does NOT
      // reserve, and a confirmation chip appears.
      await tester.tap(find.bySemanticsLabel('A2'));
      await tester.pumpAndSettle();
      expect(repo.reservedRow, isNull);
      expect(repo.reservedSeat, isNull);
      expect(find.text('Selected: Row A · Seat 2'), findsOneWidget);
      // Confirm commits the hold.
      await tester.tap(find.widgetWithText(FilledButton, 'Confirm my seat'));
      await tester.pumpAndSettle();
      expect(repo.reservedRow, 'A');
      expect(repo.reservedSeat, 2);
      // D-750 — a one-button alert (not the old seatReservedToast snackbar)
      // explaining the 3-minute pre-start check-in hold rule; the picker has
      // NOT popped yet (it waits for the acknowledgement).
      expect(find.textContaining('Seat reserved successfully'), findsOneWidget);
      expect(find.text('Reserved — pending approval'), findsNothing);
      // OK dismisses the alert and pops back to the launching screen.
      await tester.tap(find.widgetWithText(FilledButton, 'OK'));
      await tester.pumpAndSettle();
      expect(find.textContaining('Seat reserved successfully'), findsNothing);
      expect(find.text('OPEN'), findsOneWidget);
    });

    testWidgets('the Confirm CTA is disabled until a seat is selected',
        (tester) async {
      final repo = await _pump(tester);
      final confirm = tester.widget<FilledButton>(
        find.widgetWithText(FilledButton, 'Confirm my seat'),
      );
      expect(confirm.onPressed, isNull); // disabled — nothing selected yet
      expect(find.textContaining('Selected:'), findsNothing);
      // Selecting enables it.
      await tester.tap(find.bySemanticsLabel('A2'));
      await tester.pumpAndSettle();
      final enabled = tester.widget<FilledButton>(
        find.widgetWithText(FilledButton, 'Confirm my seat'),
      );
      expect(enabled.onPressed, isNotNull);
      expect(repo.reservedRow, isNull); // still not reserved (select only)
    });

    testWidgets(
        'a variable-width layout draws no phantom seats on the short row',
        (tester) async {
      await _pump(tester, repo: _FakePickerRepo(_variableMap()));
      // Row A has 4 seats, row B has 10 — A5 must not exist, B10 must.
      expect(find.bySemanticsLabel('A4'), findsOneWidget);
      expect(find.bySemanticsLabel('A5'), findsNothing);
      expect(find.bySemanticsLabel('B10'), findsOneWidget);
    });

    testWidgets('the auto-pick CTA reserves a random seat', (tester) async {
      final repo = await _pump(tester);
      await tester.tap(find.widgetWithText(FilledButton, 'Auto-pick a seat'));
      await tester.pumpAndSettle();
      expect(repo.randomCalls, 1);
    });

    testWidgets(
        'a reserve failure (after select + Confirm) shows the error '
        'toast and keeps the picker usable (busy resets, no pop)',
        (tester) async {
      await _pump(tester, repo: _FakePickerRepo(_map(), failReserve: true));
      await tester.tap(find.bySemanticsLabel('A2'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Confirm my seat'));
      await tester.pumpAndSettle();
      // The failure toast shows, and the grid is still here (the screen did not
      // pop and is not frozen — _busy was reset in the finally).
      expect(find.text("Couldn't reserve that seat"), findsOneWidget);
      expect(find.bySemanticsLabel('A2'), findsOneWidget);
    });

    testWidgets(
        'auto-pick on a FULL session shows "No places remain", not the '
        'generic failure (the capacity cap — item 4)', (tester) async {
      await _pump(tester,
          repo: _FakePickerRepo(_map(), randomSessionFull: true),);
      await tester.tap(find.widgetWithText(FilledButton, 'Auto-pick a seat'));
      await tester.pumpAndSettle();
      // The session-full message shows (not the generic seat-reserve failure),
      // and the picker stays usable.
      expect(find.text('No places remain'), findsOneWidget);
      expect(find.text("Couldn't reserve that seat"), findsNothing);
      expect(
        find.widgetWithText(FilledButton, 'Auto-pick a seat'),
        findsOneWidget,
      );
    });
  });

  group('SeatPickerScreen — change seat (B1)', () {
    testWidgets(
        'an already-held seat switches the picker to CHANGE mode: the '
        'change copy, the seat being left, and no auto-pick', (tester) async {
      await _pump(tester, repo: _FakePickerRepo(_heldMap()));

      expect(
        find.textContaining('Tap an available seat to move your booking'),
        findsOneWidget,
      );
      expect(
          find.text('Row B · Seat 1'), findsOneWidget,); // the seat being left
      expect(
        find.widgetWithText(FilledButton, 'Confirm the change'),
        findsOneWidget,
      );
      // A move is deliberate — the auto-pick lottery is not offered.
      expect(
        find.widgetWithText(FilledButton, 'Auto-pick a seat'),
        findsNothing,
      );
      expect(
        find.widgetWithText(FilledButton, 'Confirm my seat'),
        findsNothing,
      );
    });

    testWidgets(
        'confirming names the OLD and the NEW seat, then moves and pops',
        (tester) async {
      final repo = await _pump(tester, repo: _FakePickerRepo(_heldMap()));

      await tester.tap(find.bySemanticsLabel('A2'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Confirm the change'));
      await tester.pumpAndSettle();
      // The confirm step names both seats and has NOT moved anything yet.
      expect(find.text('Change your seat?'), findsOneWidget);
      expect(
        find.text('Your booking moves from Row B · Seat 1 to Row A · Seat 2.'),
        findsOneWidget,
      );
      expect(repo.movedRow, isNull);

      await tester.tap(find.widgetWithText(FilledButton, 'Change seat'));
      await tester.pumpAndSettle();
      expect(repo.movedRow, 'A');
      expect(repo.movedSeat, 2);
      expect(
        find.text('Your booking has moved to Row A · Seat 2.'),
        findsOneWidget,
      );
      await tester.tap(find.widgetWithText(FilledButton, 'OK'));
      await tester.pumpAndSettle();
      expect(find.text('OPEN'), findsOneWidget); // popped back
    });

    testWidgets('cancelling the confirm step moves nothing', (tester) async {
      final repo = await _pump(tester, repo: _FakePickerRepo(_heldMap()));

      await tester.tap(find.bySemanticsLabel('A2'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Confirm the change'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(TextButton, 'Cancel'));
      await tester.pumpAndSettle();

      expect(repo.movedRow, isNull);
      expect(
          find.bySemanticsLabel('A2'), findsOneWidget,); // still on the picker
    });

    testWidgets(
        'losing the race for the destination says the CURRENT seat is '
        'kept, and the picker stays usable', (tester) async {
      await _pump(
        tester,
        repo: _FakePickerRepo(
          _heldMap(),
          moveFailure: const ApiFailure(
            code: 'SEAT_ALREADY_RESERVED',
            message: 'taken',
            httpStatus: 409,
          ),
        ),
      );

      await tester.tap(find.bySemanticsLabel('A2'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Confirm the change'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Change seat'));
      await tester.pumpAndSettle();

      expect(
        find.text(
            'That seat was just taken — you still have your current seat.',),
        findsOneWidget,
      );
      expect(find.bySemanticsLabel('A2'), findsOneWidget);
    });

    testWidgets('a rule refusal surfaces the backend reason (session started)',
        (tester) async {
      await _pump(
        tester,
        repo: _FakePickerRepo(
          _heldMap(),
          moveFailure: const ApiFailure(
            code: 'BOOKING_SESSION_STARTED',
            message:
                'You cannot change your seat after the session has started.',
            httpStatus: 409,
          ),
        ),
      );

      await tester.tap(find.bySemanticsLabel('A2'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Confirm the change'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Change seat'));
      await tester.pumpAndSettle();

      expect(
        find.text('You cannot change your seat after the session has started.'),
        findsOneWidget,
      );
    });
  });
}
