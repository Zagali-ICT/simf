// D-771 (owner 2026-07-26) — seat TIERS on the app side: the wire decode, the
// per-row lookup, and the client-side mirror of the server's eligibility rule.
import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sessions/data/seat_enums.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';

void main() {
  group('SeatTier (D-771)', () {
    test('decodes tolerantly from an int, a name, or an unknown value', () {
      expect(SeatTier.fromJson(0), SeatTier.normal);
      expect(SeatTier.fromJson(1), SeatTier.vip);
      expect(SeatTier.fromJson(2), SeatTier.vvip);
      expect(SeatTier.fromJson('Vvip'), SeatTier.vvip);
      // Unknown / missing → Normal, which is exactly what a server that predates
      // D-771 implies by omitting the key.
      expect(SeatTier.fromJson('Platinum'), SeatTier.normal);
      expect(SeatTier.fromJson(null), SeatTier.normal);
    });

    test('mirrors the server rule: VVIP never, VIP only for VIP, Normal always',
        () {
      expect(SeatTier.vvip.selfReservableBy(callerIsVip: true), isFalse);
      expect(SeatTier.vvip.selfReservableBy(callerIsVip: false), isFalse);
      expect(SeatTier.vip.selfReservableBy(callerIsVip: true), isTrue);
      expect(SeatTier.vip.selfReservableBy(callerIsVip: false), isFalse);
      expect(SeatTier.normal.selfReservableBy(callerIsVip: true), isTrue);
      expect(SeatTier.normal.selfReservableBy(callerIsVip: false), isTrue);
    });
  });

  group('SessionSeatMap tiers (D-771)', () {
    const tiered = SessionSeatMap(
      rowLabels: <String>['A', 'B', 'C'],
      seatsPerRow: 4,
      seatTiers: <SeatTier>[SeatTier.vvip, SeatTier.vip, SeatTier.normal],
      reservedCells: <SeatCell>[],
      activeReservedCount: 0,
      hallCapacity: 12,
    );

    test('tierOfRow reads the parallel array', () {
      expect(tiered.tierOfRow(0), SeatTier.vvip);
      expect(tiered.tierOfRow(1), SeatTier.vip);
      expect(tiered.tierOfRow(2), SeatTier.normal);
    });

    test('a length-mismatched or absent tier list reads as all Normal', () {
      const degraded = SessionSeatMap(
        rowLabels: <String>['A', 'B', 'C'],
        seatsPerRow: 4,
        seatTiers: <SeatTier>[SeatTier.vvip], // shorter than rowLabels
        reservedCells: <SeatCell>[],
        activeReservedCount: 0,
        hallCapacity: 12,
      );
      expect(degraded.tierOfRow(0), SeatTier.normal);
      expect(degraded.hasTiers, isFalse);
    });

    test('canReserveRow combines the row tier with the caller VIP flag', () {
      const asNormal = SessionSeatMap(
        rowLabels: <String>['A', 'B', 'C'],
        seatsPerRow: 4,
        seatTiers: <SeatTier>[SeatTier.vvip, SeatTier.vip, SeatTier.normal],
        reservedCells: <SeatCell>[],
        activeReservedCount: 0,
        hallCapacity: 12,
      );
      expect(asNormal.canReserveRow(0), isFalse); // VVIP — nobody
      expect(asNormal.canReserveRow(1), isFalse); // VIP — not this caller
      expect(asNormal.canReserveRow(2), isTrue);

      const asVip = SessionSeatMap(
        rowLabels: <String>['A', 'B', 'C'],
        seatsPerRow: 4,
        seatTiers: <SeatTier>[SeatTier.vvip, SeatTier.vip, SeatTier.normal],
        callerIsVip: true,
        reservedCells: <SeatCell>[],
        activeReservedCount: 0,
        hallCapacity: 12,
      );
      expect(asVip.canReserveRow(0), isFalse); // still nobody
      expect(asVip.canReserveRow(1), isTrue);
      expect(asVip.canReserveRow(2), isTrue);
    });

    test('fromJson reads the appended seatTiers + callerIsVip keys', () {
      final map = SessionSeatMap.fromJson(const <String, dynamic>{
        'rowLabels': <String>['A', 'B'],
        'seatsPerRow': 2,
        'seatTiers': <int>[2, 0],
        'callerIsVip': true,
        'reservedCells': <dynamic>[],
        'activeReservedCount': 0,
        'hallCapacity': 4,
      });
      expect(map.seatTiers, <SeatTier>[SeatTier.vvip, SeatTier.normal]);
      expect(map.callerIsVip, isTrue);
      expect(map.hasTiers, isTrue);
    });

    test('an older payload without the tier keys stays all-Normal + bookable',
        () {
      final map = SessionSeatMap.fromJson(const <String, dynamic>{
        'rowLabels': <String>['A'],
        'seatsPerRow': 2,
        'reservedCells': <dynamic>[],
        'activeReservedCount': 0,
        'hallCapacity': 2,
      });
      expect(map.seatTiers, isEmpty);
      expect(map.callerIsVip, isFalse);
      expect(map.canReserveRow(0), isTrue);
    });
  });

  group('SeatCell guest hint (D-771)', () {
    test('decodes the appended hint keys and localises with a fallback', () {
      final cell = SeatCell.fromJson(const <String, dynamic>{
        'rowLabel': 'A',
        'seatNumber': 1,
        'kind': 1,
        'guestHintArabic': 'هذا المقعد محجوز لمعالي الوزير',
      });
      expect(cell.guestHintArabic, 'هذا المقعد محجوز لمعالي الوزير');
      expect(cell.guestHint, isNull);
      // English requested but only Arabic present → falls back rather than
      // blank.
      expect(cell.localizedGuestHint(isArabic: false),
          'هذا المقعد محجوز لمعالي الوزير',);
      expect(cell.localizedGuestHint(isArabic: true),
          'هذا المقعد محجوز لمعالي الوزير',);
    });

    test('an ordinary reservation carries no hint', () {
      final cell = SeatCell.fromJson(const <String, dynamic>{
        'rowLabel': 'C',
        'seatNumber': 2,
        'kind': 0,
      });
      expect(cell.localizedGuestHint(isArabic: true), isNull);
    });
  });
}
