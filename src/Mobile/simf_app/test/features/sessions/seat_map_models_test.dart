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
