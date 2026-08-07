// D-771 (owner 2026-07-26) — the staff seating desk: tap a seat -> who sits
// there (reference id, name, photo), and a badge lookup -> where they sit. Both
// answers land in the same result card. The desk must be able to inspect EVERY
// seat, including the VVIP / VIP rows a visitor may not book.
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/seat_map_repository.dart';
import 'package:simf_app/features/staff/data/staff_seating_repository.dart';
import 'package:simf_app/features/staff/staff_seating_screen.dart';

// Row A = VVIP (protocol), row B = Normal.
SessionSeatMap _map() => const SessionSeatMap(
      rowLabels: <String>['A', 'B'],
      seatsPerRow: 3,
      seatTiers: <SeatTier>[SeatTier.vvip, SeatTier.normal],
      reservedCells: <SeatCell>[
        SeatCell(
          rowLabel: 'B',
          seatNumber: 1,
          kind: SeatReservationKind.userBooking,
        ),
      ],
      activeReservedCount: 1,
      hallCapacity: 6,
      sessionTitle: 'Opening',
    );

class _FakeSeatMapRepo implements SeatMapRepository {
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

class _FakeSeatingRepo implements StaffSeatingRepository {
  _FakeSeatingRepo(this.occupant);

  final StaffSeatOccupant occupant;
  String? lookedUpRow;
  int? lookedUpSeat;
  String? lookedUpBadge;
  int photoCalls = 0;

  @override
  Future<StaffSeatOccupant> lookupSeat(
    String sessionId, {
    required String rowLabel,
    required int seatNumber,
  }) async {
    lookedUpRow = rowLabel;
    lookedUpSeat = seatNumber;
    return occupant;
  }

  @override
  Future<StaffSeatOccupant> lookupByBadge(String sessionId, String qrId) async {
    lookedUpBadge = qrId;
    return occupant;
  }

  @override
  Future<Uint8List?> occupantPhoto(String sessionId, String userId) async {
    photoCalls++;
    // No real bytes in a widget test — the card falls back to the labelled
    // placeholder, which is the point: it never reaches for Image.network.
    return null;
  }
}

Future<_FakeSeatingRepo> _pump(
  WidgetTester tester,
  StaffSeatOccupant occupant,
) async {
  tester.view.physicalSize = const Size(1000, 2400);
  tester.view.devicePixelRatio = 1.0;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  final seating = _FakeSeatingRepo(occupant);
  await tester.pumpWidget(
    ProviderScope(
      overrides: <Override>[
        seatMapRepositoryProvider.overrideWithValue(_FakeSeatMapRepo()),
        staffSeatingRepositoryProvider.overrideWithValue(seating),
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
        home: const StaffSeatingScreen(sessionId: 's1'),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return seating;
}

void main() {
  group('StaffSeatingScreen (D-771)', () {
    testWidgets('renders the desk title, the intro and the hall plan',
        (tester) async {
      await _pump(
        tester,
        const StaffSeatOccupant(
          found: false,
          tier: SeatTier.normal,
          kind: SeatReservationKind.userBooking,
          status: BookingStatus.cancelled,
          displayName: '',
          displayNameArabic: '',
          hasPhoto: false,
          checkedIn: false,
        ),
      );
      expect(find.text('Opening'), findsOneWidget);
      expect(
        find.textContaining("Scan a guest's badge to find their seat"),
        findsWidgets,
      );
    });

    testWidgets('tapping an occupied seat shows the reference, name and seat',
        (tester) async {
      final seating = await _pump(
        tester,
        const StaffSeatOccupant(
          found: true,
          rowLabel: 'B',
          seatNumber: 1,
          tier: SeatTier.normal,
          reservationId: 'RES-1234',
          kind: SeatReservationKind.userBooking,
          status: BookingStatus.approved,
          userId: 'u1',
          displayName: 'Sara Al Otaibi',
          displayNameArabic: 'سارة العتيبي',
          hasPhoto: true,
          checkedIn: true,
        ),
      );

      // B1 is reserved; the desk must still be able to inspect it.
      await tester.tap(find.bySemanticsLabel(RegExp('B1')));
      await tester.pumpAndSettle();

      expect(seating.lookedUpRow, 'B');
      expect(seating.lookedUpSeat, 1);
      expect(find.text('RES-1234'), findsOneWidget);
      expect(find.text('Sara Al Otaibi'), findsWidgets);
      expect(find.text('Row B · Seat 1'), findsOneWidget);
      expect(find.text('Checked in'), findsOneWidget);
      // The photo travels through the authenticated bytes path (D-422), never a
      // raw Image.network.
      expect(seating.photoCalls, 1);
      expect(find.byType(Image), findsNothing);
      expect(find.bySemanticsLabel('Guest photo'), findsOneWidget);
    });

    testWidgets('a VVIP seat shows the administrator guest note, not a name',
        (tester) async {
      final seating = await _pump(
        tester,
        const StaffSeatOccupant(
          found: true,
          rowLabel: 'A',
          seatNumber: 1,
          tier: SeatTier.vvip,
          reservationId: 'RES-VVIP',
          kind: SeatReservationKind.adminReservedRow,
          status: BookingStatus.approved,
          displayName: '',
          displayNameArabic: '',
          guestHint: 'Reserved for the Minister',
          guestHintArabic: 'هذا المقعد محجوز لمعالي الوزير',
          hasPhoto: false,
          checkedIn: false,
        ),
      );

      // A VVIP row is padlocked for a VISITOR but must stay inspectable here.
      await tester.tap(find.bySemanticsLabel(RegExp('A1')));
      await tester.pumpAndSettle();

      expect(seating.lookedUpRow, 'A');
      expect(find.text('Reserved for the Minister'), findsWidgets);
      expect(find.text('VVIP'), findsWidgets);
    });

    testWidgets('a badge with no seat in this session shows the no-seat state',
        (tester) async {
      final seating = await _pump(
        tester,
        const StaffSeatOccupant(
          found: false,
          tier: SeatTier.normal,
          kind: SeatReservationKind.userBooking,
          status: BookingStatus.cancelled,
          userId: 'u9',
          displayName: 'Walk In',
          displayNameArabic: 'زائر',
          hasPhoto: false,
          qrId: 'ABC123XYZ789',
          checkedIn: false,
        ),
      );

      await tester.enterText(find.byType(TextField).first, 'ABC123XYZ789');
      await tester.pumpAndSettle();
      await tester.tap(find.text('Look up'));
      await tester.pumpAndSettle();

      expect(seating.lookedUpBadge, 'ABC123XYZ789');
      expect(
        find.text('This guest has no seat in this session'),
        findsOneWidget,
      );
    });
  });
}
