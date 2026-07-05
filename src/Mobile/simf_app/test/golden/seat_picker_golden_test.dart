@Tags(<String>['golden'])
library;

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_theme.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/seat_map_repository.dart';
import 'package:simf_app/features/sessions/seat_picker_screen.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'golden_fonts.dart';

/// Render-lock golden of the Seat-Picker screen (D-485 — it has no Figma frame
/// of its own; the owner directed it to reuse the My-Seat hall card, 898:2873,
/// in its selectable configuration — D-600). Unsuffixed filename =
/// render-locked, not frame-locked.
///   flutter test --update-goldens test/golden/seat_picker_golden_test.dart
///
/// Render expected: the session title + tap-a-seat hint, the shared hall card
/// (gold المسرح·STAGE band; LTR grid — available seats GOLD-bordered as the
/// tappable cue, reserved navy-filled, no gold "mine" cell since the caller
/// holds nothing yet; 16px legend swatches), then the gold auto-pick CTA.

SessionSeatMap _map() => const SessionSeatMap(
      rowLabels: <String>['A', 'B', 'C', 'D', 'E', 'F'],
      seatsPerRow: 10,
      reservedCells: <SeatCell>[
        SeatCell(rowLabel: 'A', seatNumber: 2, kind: SeatReservationKind.userBooking),
        SeatCell(rowLabel: 'B', seatNumber: 5, kind: SeatReservationKind.userBooking),
        SeatCell(rowLabel: 'C', seatNumber: 8, kind: SeatReservationKind.userBooking),
        SeatCell(rowLabel: 'D', seatNumber: 1, kind: SeatReservationKind.userBooking),
        SeatCell(rowLabel: 'E', seatNumber: 6, kind: SeatReservationKind.userBooking),
      ],
      activeReservedCount: 5,
      hallCapacity: 60,
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
}

void main() {
  setUpAll(loadGoldenFonts);

  testWidgets('Seat-Picker @375x812 — render-lock (Arabic)', (tester) async {
    tester.view.physicalSize = const Size(375, 812);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final router = GoRouter(
      initialLocation: '/sessions/s1/pick-seat',
      routes: <RouteBase>[
        GoRoute(
          path: '/sessions/:sessionId/pick-seat',
          name: RouteNames.seatPicker,
          builder: (_, state) =>
              SeatPickerScreen(sessionId: state.pathParameters['sessionId']!),
        ),
      ],
    );

    await tester.pumpWidget(
      ProviderScope(
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
      find.byType(SeatPickerScreen),
      matchesGoldenFile('goldens/seat_picker.png'),
    );
  });
}
