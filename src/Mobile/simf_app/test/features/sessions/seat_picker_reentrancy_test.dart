import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/sessions/data/my_reservation.dart';
import 'package:simf_app/features/sessions/data/seat_enums.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/seat_map_repository.dart';
import 'package:simf_app/features/sessions/seat_picker_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../support/simf_test_scope.dart';

/// The picker's hold flow must be single-shot.
///
/// `_hold` opens with `if (_busy) return;`. Nothing rebuilds between two taps
/// in the same frame — `setState` only marks the element dirty — so that guard,
/// not the disabled CTA, is what stops the second tap. Without it an impatient
/// double-tap sends two reserve calls: the visitor holds two seats, or the
/// second call collides with the first and the screen reports a failure for a
/// booking that actually succeeded.
///
/// The fake keeps its first call PENDING on a [Completer]. An immediately
/// resolving fake would let the first `_hold` reach its `finally` (clearing
/// `_busy`) between the two taps, and the test would then pass with the guard
/// deleted — it would be measuring the fake's timing rather than the guard.

SessionSeatMap _map() => const SessionSeatMap(
      rowLabels: <String>['A', 'B'],
      seatsPerRow: 3,
      reservedCells: <SeatCell>[],
      activeReservedCount: 0,
      hallCapacity: 6,
      sessionTitle: 'Opening',
    );

class _PendingRepo implements SeatMapRepository {
  final Completer<MyReservation> gate = Completer<MyReservation>();
  int reserveCalls = 0;
  int randomCalls = 0;

  @override
  Future<SessionSeatMap> getSeatMap(String sessionId) async => _map();

  @override
  Future<MyReservation> reserveSeat(
    String sessionId, {
    required String rowLabel,
    required int seatNumber,
  }) {
    reserveCalls++;
    return gate.future;
  }

  @override
  Future<MyReservation> reserveRandom(String sessionId) {
    randomCalls++;
    return gate.future;
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
  }) =>
      throw UnimplementedError();
}

Future<_PendingRepo> _pump(WidgetTester tester) async {
  tester.view.physicalSize = const Size(1000, 2200);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  final repo = _PendingRepo();
  await tester.pumpWidget(
    simfTestScope(
      overrides: <Override>[
        seatMapRepositoryProvider.overrideWithValue(repo),
      ],
      child: const MaterialApp(
        locale: Locale('en'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: SeatPickerScreen(sessionId: 's1'),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return repo;
}

// Releases the pending call so the widget's `finally` runs and the test tears
// down without a live timer or an un-awaited future.
Future<void> _settle(WidgetTester tester, _PendingRepo repo) async {
  repo.gate.complete(
    const MyReservation(
      reservationId: 'r1',
      sessionId: 's1',
      rowLabel: 'A',
      seatNumber: 2,
      kind: SeatReservationKind.userBooking,
      status: BookingStatus.approved,
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('the picker holds a seat once per tap burst', () {
    testWidgets('a double-tapped auto-pick sends ONE reserveRandom',
        (tester) async {
      final repo = await _pump(tester);
      final autoPick = find.widgetWithText(FilledButton, 'Auto-pick a seat');

      // Both taps land inside the same frame — no pump between them — which is
      // exactly what a real double-tap does and what the `_busy` guard exists
      // to absorb.
      await tester.tap(autoPick);
      await tester.tap(autoPick, warnIfMissed: false);
      await tester.pump();

      expect(
        repo.randomCalls,
        1,
        reason: 'Two calls means the re-entrancy guard is gone: the visitor is '
            'assigned two seats, or the second request collides with the first '
            'and the screen reports a failure for a booking that succeeded.',
      );

      await _settle(tester, repo);
    });

    testWidgets('a double-tapped Confirm sends ONE reserveSeat',
        (tester) async {
      final repo = await _pump(tester);

      await tester.tap(
        find.descendant(
          of: find.byType(SeatPickerScreen),
          matching: find.text('2').first,
        ),
      );
      await tester.pumpAndSettle();

      final confirm = find.widgetWithText(FilledButton, 'Confirm my seat');
      await tester.tap(confirm);
      await tester.tap(confirm, warnIfMissed: false);
      await tester.pump();

      expect(repo.reserveCalls, 1);

      await _settle(tester, repo);
    });

    // The control: with the first call finished, a genuine second attempt must
    // still go through. A guard that latched `_busy` on forever would pass the
    // two tests above and leave the picker permanently dead.
    testWidgets('a tap AFTER the first call settles is still accepted',
        (tester) async {
      final repo = await _pump(tester);
      final autoPick = find.widgetWithText(FilledButton, 'Auto-pick a seat');

      await tester.tap(autoPick);
      await tester.pump();
      expect(repo.randomCalls, 1);

      // Fail the in-flight call so the screen un-freezes without navigating
      // away, then tap again. An ApiFailure, because that is the one failure
      // the hold flow catches; anything else escapes as an unhandled error.
      repo.gate.completeError(
        const ApiFailure(code: 'NETWORK', message: 'offline', httpStatus: 0),
      );
      await tester.pumpAndSettle();

      await tester.tap(autoPick);
      await tester.pump();
      expect(repo.randomCalls, 2);
    });
  });
}
