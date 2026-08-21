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

/// Pins the `_busy` re-entrancy guard in `_hold`: nothing rebuilds between two
/// taps in one frame, so the guard and not the disabled CTA stops the second.
/// The fake must hold its first call PENDING or the guard can be deleted.

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

// Releases the pending call so the tear-down has no live timer left.
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

      // No pump between the taps: they must land inside the same frame.
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

    // The control: a guard that latched `_busy` on forever would pass both
    // tests above and leave the picker permanently dead.
    testWidgets('a tap AFTER the first call settles is still accepted',
        (tester) async {
      final repo = await _pump(tester);
      final autoPick = find.widgetWithText(FilledButton, 'Auto-pick a seat');

      await tester.tap(autoPick);
      await tester.pump();
      expect(repo.randomCalls, 1);

      // An ApiFailure specifically: the hold flow catches nothing else, so
      // anything else escapes as an unhandled error.
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
