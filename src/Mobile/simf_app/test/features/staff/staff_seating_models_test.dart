import 'package:flutter_test/flutter_test.dart';
import 'package:simf_app/features/sessions/data/seat_enums.dart';
import 'package:simf_app/features/staff/data/staff_seating_models.dart';

/// `StaffSeatOccupant.fromJson` (D-771) is tolerant, so a lookup response that
/// OMITS `found` decodes to the fallback rather than throwing.
///
/// `found` is not a label: the staff seating desk renders the occupant card —
/// name, photo, check-in control — only when it is true, and the "no seat" /
/// "seat empty" message otherwise. Defaulting it TRUE would present a card
/// built entirely out of the OTHER fallbacks (an empty name, tier Normal,
/// status Pending) as a real occupant, which is a staff member being told a
/// badge holds a seat it does not.
void main() {
  group('StaffSeatOccupant — found defaults to NOT found', () {
    test('a real occupant decodes every key, not a fallback', () {
      final occupant = StaffSeatOccupant.fromJson(const <String, dynamic>{
        'found': true,
        'rowLabel': 'B',
        'seatNumber': 12,
        'tier': 2,
        'reservationId': 'r-1',
        'kind': 1,
        'status': 1,
        'userId': 'u-1',
        'displayName': 'Rakan Alsalem',
        'displayNameArabic': 'راكان السالم',
        'guestHint': 'Ministry guest',
        'guestHintArabic': 'ضيف الوزارة',
        'hasPhoto': true,
        'qrId': 'ABCDEFGH1234',
        'checkedIn': true,
      });

      expect(occupant.found, isTrue);
      expect(occupant.rowLabel, 'B');
      expect(occupant.seatNumber, 12);
      expect(occupant.tier, SeatTier.vvip);
      expect(occupant.reservationId, 'r-1');
      expect(occupant.kind, SeatReservationKind.adminReservedRow);
      expect(occupant.status, BookingStatus.approved);
      expect(occupant.userId, 'u-1');
      expect(occupant.localizedName(isArabic: true), 'راكان السالم');
      expect(occupant.localizedGuestHint(isArabic: false), 'Ministry guest');
      expect(occupant.hasPhoto, isTrue);
      expect(occupant.qrId, 'ABCDEFGH1234');
      expect(occupant.checkedIn, isTrue);
    });

    test('an ABSENT found is an empty seat, not an occupant', () {
      // The shape a "nothing here" response really has: the seat coordinates
      // come back, the occupant half does not.
      final occupant = StaffSeatOccupant.fromJson(const <String, dynamic>{
        'rowLabel': 'B',
        'seatNumber': 12,
        'tier': 0,
      });

      expect(occupant.found, isFalse);
      // Everything the card would have shown is blank, which is why a wrong
      // `found` renders a nameless ghost occupant rather than failing loudly.
      expect(occupant.displayName, '');
      expect(occupant.displayNameArabic, '');
      expect(occupant.localizedName(isArabic: true), '');
      expect(occupant.hasPhoto, isFalse);
      expect(occupant.checkedIn, isFalse);
    });

    test('an explicitly NULL found is not found', () {
      final occupant = StaffSeatOccupant.fromJson(const <String, dynamic>{
        'found': null,
        'displayName': null,
        'checkedIn': null,
        'hasPhoto': null,
      });

      expect(occupant.found, isFalse);
      expect(occupant.displayName, '');
      expect(occupant.checkedIn, isFalse);
      expect(occupant.hasPhoto, isFalse);
    });

    test('an empty response defaults every field to the closed value', () {
      final occupant = StaffSeatOccupant.fromJson(const <String, dynamic>{});

      expect(occupant.found, isFalse);
      expect(occupant.rowLabel, isNull);
      expect(occupant.seatNumber, isNull);
      expect(occupant.tier, SeatTier.normal);
      expect(occupant.kind, SeatReservationKind.userBooking);
      expect(occupant.status, BookingStatus.pending);
      expect(occupant.reservationId, isNull);
      expect(occupant.userId, isNull);
      expect(occupant.qrId, isNull);
      expect(occupant.localizedGuestHint(isArabic: true), isNull);
    });
  });
}
