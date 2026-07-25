import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/widgets/hall_seat_map.dart';

/// Widget tests for the shared [HallSeatMapCard]: per-row variable seat counts
/// (Option A) render each row at its own width with no phantom seats, every
/// available cell carries its seat number, an absent seatCounts stays uniform,
/// and an available seat is tappable with the correct row + seat.

Future<void> _pump(
  WidgetTester tester,
  SessionSeatMap map, {
  void Function(String rowLabel, int seatNumber)? onSeatTap,
}) async {
  tester.view.physicalSize = const Size(1200, 1600);
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
        body: SingleChildScrollView(
          child: Builder(
            builder: (context) => HallSeatMapCard(
              map: map,
              l10n: AppL10n.of(context),
              onSeatTap: onSeatTap,
            ),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

SessionSeatMap _variableMap() => const SessionSeatMap(
      rowLabels: <String>['A', 'B', 'C', 'D'],
      seatsPerRow: 10, // legacy fallback = max(counts)
      seatCounts: <int>[4, 10, 8, 8],
      reservedCells: <SeatCell>[],
      activeReservedCount: 0,
      hallCapacity: 30,
    );

void main() {
  group('HallSeatMapCard variable widths', () {
    testWidgets('draws each row at its own count (no phantom short-row seats)',
        (tester) async {
      await _pump(tester, _variableMap(), onSeatTap: (_, __) {});

      // Row A has 4 seats — A4 exists, A5 does not.
      expect(find.bySemanticsLabel('A4'), findsOneWidget);
      expect(find.bySemanticsLabel('A5'), findsNothing);
      // Row B has 10 — B10 exists.
      expect(find.bySemanticsLabel('B10'), findsOneWidget);
      // Row C has 8 — C8 exists, C9 does not.
      expect(find.bySemanticsLabel('C8'), findsOneWidget);
      expect(find.bySemanticsLabel('C9'), findsNothing);
    });

    testWidgets('renders the seat number in every available cell',
        (tester) async {
      await _pump(tester, _variableMap());

      // Seat 10 is unique to row B (the only 10-wide row), so its numeral must
      // render exactly once — proof the number is drawn per available cell.
      expect(find.text('10'), findsOneWidget);
      // Seat 1 appears once per row (4 rows).
      expect(find.text('1'), findsNWidgets(4));
    });

    testWidgets('an absent seatCounts renders a uniform grid', (tester) async {
      const uniform = SessionSeatMap(
        rowLabels: <String>['A', 'B'],
        seatsPerRow: 3,
        reservedCells: <SeatCell>[],
        activeReservedCount: 0,
        hallCapacity: 6,
      );
      await _pump(tester, uniform, onSeatTap: (_, __) {});

      // Each row draws exactly 3 seats — A3 exists, A4 does not.
      expect(find.bySemanticsLabel('A3'), findsOneWidget);
      expect(find.bySemanticsLabel('A4'), findsNothing);
      expect(find.bySemanticsLabel('B3'), findsOneWidget);
    });

    testWidgets('a tappable available seat reports the correct row + seat',
        (tester) async {
      String? tappedRow;
      int? tappedSeat;
      await _pump(
        tester,
        _variableMap(),
        onSeatTap: (row, seat) {
          tappedRow = row;
          tappedSeat = seat;
        },
      );

      await tester.tap(find.bySemanticsLabel('C7'));
      await tester.pumpAndSettle();
      expect(tappedRow, 'C');
      expect(tappedSeat, 7);
    });

    testWidgets('a tall hall gets a two-axis (horizontal + vertical) scroll',
        (tester) async {
      final tall = SessionSeatMap(
        rowLabels: List<String>.generate(15, (i) => 'R${i + 1}'),
        seatsPerRow: 20,
        reservedCells: const <SeatCell>[],
        activeReservedCount: 0,
        hallCapacity: 300,
      );
      await _pump(tester, tall);

      // The seat grid pans on BOTH axes inside the card: a vertical view (rows)
      // wraps a horizontal view (seats), so a hall taller/wider than the card
      // scrolls up/down and left/right rather than overflowing.
      final scrolls = tester.widgetList<SingleChildScrollView>(
        find.descendant(
          of: find.byType(HallSeatMapCard),
          matching: find.byType(SingleChildScrollView),
        ),
      );
      expect(scrolls.any((s) => s.scrollDirection == Axis.vertical), isTrue);
      expect(scrolls.any((s) => s.scrollDirection == Axis.horizontal), isTrue);
    });
  });
}
