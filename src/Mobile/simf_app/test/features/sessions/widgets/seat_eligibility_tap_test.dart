import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/sessions/data/seat_enums.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/widgets/hall_seat_map.dart';
import 'package:simf_app/features/sessions/widgets/seat_grid_row.dart';

/// D-771 — a seat this caller may not book must be inert; [HallSeatMapCard]
/// gates per row and [SeatGridRow] per seat, and the two break independently.
/// Seats are found structurally, not by spoken label, since the label is
/// derived from the state under test.

// Row A normal (A1 already held by someone else), row B VIP, row C VVIP.
// The caller is not a VIP, so only row A is theirs to book.
const _tieredMap = SessionSeatMap(
  rowLabels: <String>['A', 'B', 'C'],
  seatsPerRow: 3,
  seatTiers: <SeatTier>[SeatTier.normal, SeatTier.vip, SeatTier.vvip],
  reservedCells: <SeatCell>[
    SeatCell(
      rowLabel: 'A',
      seatNumber: 1,
      kind: SeatReservationKind.userBooking,
    ),
  ],
  activeReservedCount: 1,
  hallCapacity: 9,
);

Future<List<String>> _pump(
  WidgetTester tester, {
  bool inspectMode = false,
}) async {
  tester.view.physicalSize = const Size(1200, 1600);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  final taps = <String>[];
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
        body: SingleChildScrollView(
          child: Builder(
            builder: (context) => HallSeatMapCard(
              map: _tieredMap,
              l10n: AppL10n.of(context),
              inspectMode: inspectMode,
              onSeatTap: (row, seat) => taps.add('$row$seat'),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return taps;
}

Finder _seat(String rowLabel, int seatNumber) => find.descendant(
      of: find.byWidgetPredicate(
        (widget) => widget is SeatGridRow && widget.rowLabel == rowLabel,
      ),
      matching: find.byWidgetPredicate(
        (widget) => widget is SeatBox && widget.seatNumber == seatNumber,
      ),
    );

Future<void> _tapSeat(WidgetTester tester, String row, int seat) async {
  await tester.tap(_seat(row, seat));
  await tester.pumpAndSettle();
}

void main() {
  group('a seat the caller may not reserve is not offered', () {
    // Positive control: without it the "no callback" assertions below would
    // also pass against a harness that never lands a tap.
    testWidgets('a free seat in a bookable row DOES report its tap',
        (tester) async {
      final taps = await _pump(tester);

      await _tapSeat(tester, 'A', 2);

      expect(taps, <String>['A2']);
    });

    testWidgets('a free seat in a VIP row is inert for a non-VIP caller',
        (tester) async {
      final taps = await _pump(tester);

      await _tapSeat(tester, 'B', 1);

      expect(
        taps,
        isEmpty,
        reason: 'B is a VIP row and this caller is not VIP, so the seat is '
            'locked. Reporting the tap means the picker sends a reserve the '
            'server refuses — or worse, holds a seat the venue has already '
            'promised to someone else.',
      );
    });

    testWidgets('a free seat in a VVIP protocol row is inert for everyone',
        (tester) async {
      final taps = await _pump(tester);

      await _tapSeat(tester, 'C', 3);

      expect(taps, isEmpty, reason: 'VVIP seating is assigned, never booked.');
    });

    // The other half of `tappable`: no tier, simply taken.
    testWidgets('a seat already held by someone else is inert', (tester) async {
      final taps = await _pump(tester);

      await _tapSeat(tester, 'A', 1);

      expect(
        taps,
        isEmpty,
        reason: 'A1 is reserved. Offering it as tappable is the double-booking '
            'path: two visitors both told the seat is theirs.',
      );
    });

    // Locked must also be announced, not merely inert.
    testWidgets('a locked seat says why', (tester) async {
      await _pump(tester);

      expect(
        find.bySemanticsLabel('B1 · Reserved for VIP guests'),
        findsOneWidget,
      );
      expect(
        find.bySemanticsLabel(
          'C1 · Reserved for protocol guests — cannot be booked',
        ),
        findsOneWidget,
      );
    });

    // D-771 — the staff desk looks occupants up, so there the same seats
    // ARE tappable.
    testWidgets('inspect mode (the staff desk) can still tap a locked seat',
        (tester) async {
      final taps = await _pump(tester, inspectMode: true);

      await _tapSeat(tester, 'C', 3);
      await _tapSeat(tester, 'A', 1);

      expect(taps, <String>['C3', 'A1']);
    });
  });
}
