@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sessions/data/seat_enums.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/widgets/hall_seat_map.dart';

import 'golden_fonts.dart';

/// Render-lock golden of the shared [HallSeatMapCard] for a LARGE hall (20 rows
/// x 25 seats), proving the same-day 2-axis-scroll refinement (D-767): seats
/// draw at a FIXED size inside a bounded ~8-row viewport that shows BOTH a
/// vertical scrollbar (more rows below) and a horizontal one (more seats to the
/// right), instead of shrinking 25 seats into an unreadable row. The mine
/// (gold)
/// + reserved (navy x) cells stay legible. flutter test --update-goldens
///   test/golden/seat_map_scroll_golden_test.dart

SessionSeatMap _bigHall() {
  final rows = <String>[
    'VVIP',
    'VIP01',
    'VIP02',
    for (var i = 1; i <= 17; i++) 'A${i.toString().padLeft(3, '0')}',
  ];
  return SessionSeatMap(
    rowLabels: rows,
    seatsPerRow: 25,
    reservedCells: const <SeatCell>[
      SeatCell(
        rowLabel: 'VVIP',
        seatNumber: 3,
        kind: SeatReservationKind.adminReservedRow,
      ),
      SeatCell(
        rowLabel: 'VIP01',
        seatNumber: 10,
        kind: SeatReservationKind.userBooking,
      ),
      SeatCell(
        rowLabel: 'A002',
        seatNumber: 6,
        kind: SeatReservationKind.userBooking,
      ),
    ],
    myCell: const SeatCell(
      rowLabel: 'A004',
      seatNumber: 9,
      kind: SeatReservationKind.userBooking,
    ),
    activeReservedCount: 4,
    hallCapacity: 500,
  );
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('HallSeatMapCard @375 — large hall, 2-axis scroll (Arabic)',
      (tester) async {
    tester.view.physicalSize = const Size(375, 540);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      MaterialApp(
        debugShowCheckedModeBanner: false,
        theme: SimfTheme.dark(),
        locale: const Locale('ar'),
        supportedLocales: AppL10n.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          ...AppL10n.localizationsDelegates,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: Scaffold(
          backgroundColor: SimfTokens.navy,
          body: SingleChildScrollView(
            child: Padding(
              padding: const EdgeInsets.all(SimfTokens.space4),
              child: Builder(
                builder: (context) => HallSeatMapCard(
                  map: _bigHall(),
                  l10n: AppL10n.of(context),
                  onSeatTap: (_, __) {},
                  maxSeatSize: SimfTokens.seatCapPicker,
                  availableBorderColor: SimfTokens.accent,
                  swatchSize: SimfTokens.seatSwatchLg,
                ),
              ),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(HallSeatMapCard),
      matchesGoldenFile('goldens/seat_map_scroll.png'),
    );
  });
}
