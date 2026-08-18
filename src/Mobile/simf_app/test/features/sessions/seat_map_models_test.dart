import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sessions/data/my_reservation.dart';
import 'package:simf_app/features/sessions/data/seat_enums.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';

void main() {
  group('SeatReservationKind.fromJson', () {
    test('decodes int / name; unknown → userBooking', () {
      expect(SeatReservationKind.fromJson(1),
          SeatReservationKind.adminReservedRow,);
      expect(SeatReservationKind.fromJson(2),
          SeatReservationKind.randomAssignment,);
      expect(
        SeatReservationKind.fromJson('AdminReservedRow'),
        SeatReservationKind.adminReservedRow,
      );
      expect(SeatReservationKind.fromJson(9), SeatReservationKind.userBooking);
      expect(SeatReservationKind.adminReservedRow.isAdminBlock, isTrue);
      // D-485 — the open-seating (general-admission) kind.
      expect(SeatReservationKind.fromJson(3), SeatReservationKind.openSeating);
      expect(
        SeatReservationKind.fromJson('OpenSeating'),
        SeatReservationKind.openSeating,
      );
    });
  });

  group('SeatSelectionMode.fromJson (D-485)', () {
    test('decodes int / name; unknown → assignedSeat', () {
      expect(SeatSelectionMode.fromJson(0), SeatSelectionMode.assignedSeat);
      expect(SeatSelectionMode.fromJson(1), SeatSelectionMode.openSeating);
      expect(
        SeatSelectionMode.fromJson('OpenSeating'),
        SeatSelectionMode.openSeating,
      );
      // An older server that omits the field → seat-assigned (the safe
      // default).
      expect(SeatSelectionMode.fromJson(null), SeatSelectionMode.assignedSeat);
      expect(SeatSelectionMode.fromJson(9), SeatSelectionMode.assignedSeat);
      expect(SeatSelectionMode.openSeating.isOpenSeating, isTrue);
    });
  });

  group('BookingStatus.fromJson (D-485)', () {
    test('decodes int / name; unknown → pending', () {
      expect(BookingStatus.fromJson(0), BookingStatus.pending);
      expect(BookingStatus.fromJson(1), BookingStatus.approved);
      expect(BookingStatus.fromJson('Rejected'), BookingStatus.rejected);
      expect(BookingStatus.fromJson(99), BookingStatus.pending);
    });
  });

  group('MyReservation.fromJson (D-485)', () {
    test('a seat booking carries row/seat and the status', () {
      final r = MyReservation.fromJson(const <String, dynamic>{
        'reservationId': 'r1',
        'sessionId': 's1',
        'rowLabel': 'A',
        'seatNumber': 5,
        'kind': 0,
        'status': 0,
      });
      expect(r.rowLabel, 'A');
      expect(r.seatNumber, 5);
      expect(r.kind, SeatReservationKind.userBooking);
      expect(r.status, BookingStatus.pending);
      expect(r.isOpenSeating, isFalse);
    });

    test('an open-seating join has null row/seat', () {
      final r = MyReservation.fromJson(const <String, dynamic>{
        'reservationId': 'r2',
        'sessionId': 's1',
        'rowLabel': null,
        'seatNumber': null,
        'kind': 3,
        'status': 0,
      });
      expect(r.rowLabel, isNull);
      expect(r.seatNumber, isNull);
      expect(r.isOpenSeating, isTrue);
    });
  });

  group('SessionSeatMap.fromJson', () {
    final map = SessionSeatMap.fromJson(const <String, dynamic>{
      'sessionId': 's1',
      'hallId': 'h1',
      'hallCapacity': 6,
      'sessionCapacity': null,
      'rowLabels': <dynamic>['A', 'B'],
      'seatsPerRow': 3,
      'reservedCells': <dynamic>[
        <String, dynamic>{
          'reservationId': 'r1',
          'rowLabel': 'A',
          'seatNumber': 1,
          'kind': 1,
        },
      ],
      'myCell': <String, dynamic>{
        'reservationId': 'r2',
        'rowLabel': 'B',
        'seatNumber': 2,
        'kind': 0,
      },
      'activeReservedCount': 2,
    });

    test('binds the grid + derives status', () {
      expect(map.hasLayout, isTrue);
      expect(map.rowLabels, <String>['A', 'B']);
      expect(map.seatsPerRow, 3);
      expect(map.capacity, 6); // sessionCapacity null → hallCapacity
      expect(map.reservedKeys(), contains('A:1'));
      expect(map.isMine('B', 2), isTrue);
      expect(map.isMine('A', 1), isFalse);
      expect(map.myCell!.kind, SeatReservationKind.userBooking);
      expect(
          map.reservedCells.single.kind, SeatReservationKind.adminReservedRow,);
      // D-485 — no 'mode' key on the wire → the safe assigned-seat default.
      expect(map.mode, SeatSelectionMode.assignedSeat);
    });

    test('binds the open-seating mode when present (D-485)', () {
      final open = SessionSeatMap.fromJson(const <String, dynamic>{
        'rowLabels': <dynamic>[],
        'seatsPerRow': 0,
        'reservedCells': <dynamic>[],
        'activeReservedCount': 0,
        'hallCapacity': 200,
        'mode': 1,
      });
      expect(open.mode, SeatSelectionMode.openSeating);
    });

    test('an empty layout has no grid', () {
      final empty = SessionSeatMap.fromJson(const <String, dynamic>{
        'rowLabels': <dynamic>[],
        'seatsPerRow': 0,
        'reservedCells': <dynamic>[],
        'activeReservedCount': 0,
        'hallCapacity': 0,
      });
      expect(empty.hasLayout, isFalse);
      expect(empty.myCell, isNull);
    });
  });

  // A12 / DEF-SEA-002 — the fourth seat state. `checkedIn` has shipped on the
  // wire since Wave 2 but SeatCell.fromJson dropped it, so the app could never
  // tell a merely-held seat from a confirmed (تم التأكيد) one. These fail
  // against the old decode.
  group('SeatCell checkedIn (A12)', () {
    test('decodes the checkedIn wire key', () {
      final confirmed = SeatCell.fromJson(const <String, dynamic>{
        'reservationId': 'r1',
        'rowLabel': 'A',
        'seatNumber': 1,
        'kind': 0,
        'status': 1,
        'checkedIn': true,
      });
      expect(confirmed.checkedIn, isTrue);
    });

    test('an omitted checkedIn key reads as not-yet-arrived', () {
      final held = SeatCell.fromJson(const <String, dynamic>{
        'reservationId': 'r2',
        'rowLabel': 'A',
        'seatNumber': 2,
        'kind': 0,
      });
      expect(held.checkedIn, isFalse);
    });

    test('the map exposes the cell by key and flags a confirmed hall', () {
      final map = SessionSeatMap.fromJson(const <String, dynamic>{
        'rowLabels': <dynamic>['A'],
        'seatsPerRow': 2,
        'reservedCells': <dynamic>[
          <String, dynamic>{
            'reservationId': 'r1',
            'rowLabel': 'A',
            'seatNumber': 1,
            'kind': 0,
            'checkedIn': true,
          },
          <String, dynamic>{
            'reservationId': 'r2',
            'rowLabel': 'A',
            'seatNumber': 2,
            'kind': 0,
            'checkedIn': false,
          },
        ],
        'activeReservedCount': 2,
        'hallCapacity': 2,
      });
      expect(map.reservedByKey()['A:1']!.checkedIn, isTrue);
      expect(map.reservedByKey()['A:2']!.checkedIn, isFalse);
      expect(map.hasConfirmed, isTrue);
    });

    test('a hall with nobody through the gate is not confirmed', () {
      final map = SessionSeatMap.fromJson(const <String, dynamic>{
        'rowLabels': <dynamic>['A'],
        'seatsPerRow': 1,
        'reservedCells': <dynamic>[
          <String, dynamic>{
            'reservationId': 'r1',
            'rowLabel': 'A',
            'seatNumber': 1,
            'kind': 0,
          },
        ],
        'activeReservedCount': 1,
        'hallCapacity': 1,
      });
      expect(map.hasConfirmed, isFalse);
    });
  });

  group('SessionSeatMap per-row seat counts (Option A)', () {
    test('parses seatCounts and reads each row at its own width', () {
      final map = SessionSeatMap.fromJson(const <String, dynamic>{
        'rowLabels': <dynamic>['A', 'B', 'C', 'D'],
        'seatsPerRow': 10, // legacy fallback = max(counts)
        'seatCounts': <dynamic>[4, 10, 8, 8],
        'reservedCells': <dynamic>[],
        'activeReservedCount': 0,
        'hallCapacity': 30,
      });
      expect(map.seatCounts, <int>[4, 10, 8, 8]);
      expect(map.seatsInRow(0), 4);
      expect(map.seatsInRow(1), 10);
      expect(map.seatsInRow(2), 8);
      expect(map.seatsInRow(3), 8);
      expect(map.maxSeatsPerRow, 10);
      expect(map.hasLayout, isTrue);
    });

    test('an absent seatCounts key stays uniform (back-compat)', () {
      final map = SessionSeatMap.fromJson(const <String, dynamic>{
        'rowLabels': <dynamic>['A', 'B'],
        'seatsPerRow': 3,
        'reservedCells': <dynamic>[],
        'activeReservedCount': 0,
        'hallCapacity': 6,
      });
      expect(map.seatCounts, isEmpty);
      expect(map.seatsInRow(0), 3);
      expect(map.seatsInRow(1), 3);
      expect(map.maxSeatsPerRow, 3);
    });

    test('a zero seatsPerRow still has a layout when counts are present', () {
      final map = SessionSeatMap.fromJson(const <String, dynamic>{
        'rowLabels': <dynamic>['A', 'B'],
        'seatsPerRow': 0,
        'seatCounts': <dynamic>[4, 6],
        'reservedCells': <dynamic>[],
        'activeReservedCount': 0,
        'hallCapacity': 10,
      });
      expect(map.hasLayout, isTrue);
      expect(map.seatsInRow(0), 4);
      expect(map.seatsInRow(1), 6);
      expect(map.maxSeatsPerRow, 6);
    });

    test('a length-mismatched seatCounts falls back to the uniform width', () {
      final map = SessionSeatMap.fromJson(const <String, dynamic>{
        'rowLabels': <dynamic>['A', 'B'],
        'seatsPerRow': 5,
        'seatCounts': <dynamic>[4, 6, 8], // 3 counts for 2 rows
        'reservedCells': <dynamic>[],
        'activeReservedCount': 0,
        'hallCapacity': 10,
      });
      expect(map.seatsInRow(0), 5); // fell back to seatsPerRow
      expect(map.seatsInRow(1), 5);
      expect(map.maxSeatsPerRow, 5);
    });

    test(
        'a length-mismatched seatCounts with a zero seatsPerRow reports no '
        'layout (degraded response falls to the safe empty state, never a '
        'zero-column grid)', () {
      final map = SessionSeatMap.fromJson(const <String, dynamic>{
        'rowLabels': <dynamic>['A', 'B', 'C'],
        'seatsPerRow': 0,
        'seatCounts': <dynamic>[5, 3], // 2 counts for 3 rows (one dropped)
        'reservedCells': <dynamic>[],
        'activeReservedCount': 0,
        'hallCapacity': 8,
      });
      expect(map.maxSeatsPerRow, 0);
      expect(map.hasLayout, isFalse);
    });
  });
}
