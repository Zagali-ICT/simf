import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';

void main() {
  group('SeatReservationKind.fromJson', () {
    test('decodes int / name; unknown → userBooking', () {
      expect(SeatReservationKind.fromJson(1), SeatReservationKind.adminReservedRow);
      expect(SeatReservationKind.fromJson(2), SeatReservationKind.randomAssignment);
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
      // An older server that omits the field → seat-assigned (the safe default).
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
      final r = MyReservation.fromJson(<String, dynamic>{
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
      final r = MyReservation.fromJson(<String, dynamic>{
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
    final map = SessionSeatMap.fromJson(<String, dynamic>{
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
      expect(map.reservedCells.single.kind, SeatReservationKind.adminReservedRow);
      // D-485 — no 'mode' key on the wire → the safe assigned-seat default.
      expect(map.mode, SeatSelectionMode.assignedSeat);
    });

    test('binds the open-seating mode when present (D-485)', () {
      final open = SessionSeatMap.fromJson(<String, dynamic>{
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
      final empty = SessionSeatMap.fromJson(<String, dynamic>{
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
}
