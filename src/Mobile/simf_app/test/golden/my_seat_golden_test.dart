@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/misc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/sessions/data/my_reservation.dart';
import 'package:simf_app/features/sessions/data/seat_enums.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/seat_map_repository.dart';
import 'package:simf_app/features/sessions/my_seat_screen.dart';

import '../support/simf_test_scope.dart';
import 'golden_fonts.dart';

/// Golden render of the My-Seat screen against Figma frame **898:2873** ("Your
/// seat", approved Visitor). Compare to the frame: flutter test
/// --update-goldens test/golden/my_seat_golden_test.dart
///
/// Frame parity expected: the "الجلسة" card (label, session title, the الصف
/// gold-bordered chip at the inline-start/right + the مقعد beige chip), the
/// hall card (gold المسرح·STAGE band, the LTR seat grid — a per-row VARIABLE
/// layout so short rows draw fewer seats centred under the stage; mine gold
/// with the ✓ icon, reserved navy-filled with the × icon, available
/// beige-bordered with its seat number, squares ≤20px — and the LTR
/// محجوز·متاح·مقعدك legend), then the gold إرشادي إلى مقعدي + outlined مشاركة
/// الموقع action row. The seat-occupancy PATTERN is fixture data — the chrome
/// is the parity claim. Rendered via the shared `HallSeatMapCard` (D-600).

SessionSeatMap _map() => const SessionSeatMap(
      rowLabels: <String>['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'],
      seatsPerRow: 12, // legacy fallback = max(counts)
      seatCounts: <int>[10, 12, 11, 6, 8, 4, 9, 6],
      reservedCells: <SeatCell>[
        SeatCell(
            rowLabel: 'A',
            seatNumber: 1,
            kind: SeatReservationKind.userBooking,),
        SeatCell(
            rowLabel: 'A',
            seatNumber: 5,
            kind: SeatReservationKind.userBooking,),
        SeatCell(
            rowLabel: 'A',
            seatNumber: 9,
            kind: SeatReservationKind.userBooking,),
        SeatCell(
            rowLabel: 'B',
            seatNumber: 2,
            kind: SeatReservationKind.userBooking,),
        SeatCell(
            rowLabel: 'B',
            seatNumber: 6,
            kind: SeatReservationKind.userBooking,),
        SeatCell(
            rowLabel: 'C',
            seatNumber: 3,
            kind: SeatReservationKind.userBooking,),
        SeatCell(
            rowLabel: 'C',
            seatNumber: 11,
            kind: SeatReservationKind.userBooking,),
        SeatCell(
            rowLabel: 'D',
            seatNumber: 4,
            kind: SeatReservationKind.userBooking,),
        SeatCell(
            rowLabel: 'E',
            seatNumber: 7,
            kind: SeatReservationKind.userBooking,),
        SeatCell(
            rowLabel: 'F',
            seatNumber: 1,
            kind: SeatReservationKind.userBooking,),
        SeatCell(
            rowLabel: 'G',
            seatNumber: 8,
            kind: SeatReservationKind.userBooking,),
        SeatCell(
            rowLabel: 'H',
            seatNumber: 5,
            kind: SeatReservationKind.userBooking,),
      ],
      myCell: SeatCell(
        rowLabel: 'B',
        seatNumber: 12,
        kind: SeatReservationKind.userBooking,
      ),
      activeReservedCount: 13,
      hallCapacity: 96,
      sessionTitleArabic: 'أمن سلاسل إمداد الطاقة البحرية',
      sessionTitle: 'Maritime Energy Supply-Chain Security',
    );

class _FakeSeatRepo implements SeatMapRepository {
  @override
  Future<SessionSeatMap> getSeatMap(String sessionId) async => _map();

  @override
  Future<MyReservation> reserveSeat(
    String sessionId, {
    required String rowLabel,
    required int seatNumber,
  }) =>
      throw UnimplementedError();

  @override
  Future<MyReservation> reserveRandom(String sessionId) =>
      throw UnimplementedError();

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

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('My-Seat @375x939 — Figma 898:2873 (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 939);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/sessions/s1/my-seat',
      routes: <RouteBase>[
        GoRoute(
          path: '/sessions/:sessionId/my-seat',
          name: RouteNames.mySeat,
          builder: (_, state) =>
              MySeatScreen(sessionId: state.pathParameters['sessionId']!),
        ),
        GoRoute(
          path: '/map',
          name: RouteNames.venueMap,
          builder: (_, __) => const Scaffold(body: SizedBox.shrink()),
        ),
      ],
    );

    await tester.pumpWidget(
      simfTestScope(
        overrides: <Override>[
          seatMapRepositoryProvider.overrideWithValue(_FakeSeatRepo()),
        ],
        child: MaterialApp.router(
          debugShowCheckedModeBanner: false,
          theme: SimfTheme.dark(),
          routerConfig: router,
          locale: const Locale('ar'),
          supportedLocales: AppL10n.supportedLocales,
          localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
            ...AppL10n.localizationsDelegates,
            GlobalMaterialLocalizations.delegate,
            GlobalWidgetsLocalizations.delegate,
            GlobalCupertinoLocalizations.delegate,
          ],
        ),
      ),
    );
    await tester.pumpAndSettle();

    await expectLater(
      find.byType(MySeatScreen),
      matchesGoldenFile('goldens/my_seat_898-2873.png'),
    );
  });
}
