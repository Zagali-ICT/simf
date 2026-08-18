/// The int-backed wire enums of the seating contract: how a reservation came
/// to exist ([SeatReservationKind]), how attendees join ([SeatSelectionMode]),
/// the tier of a layout row ([SeatTier]) and the approval state of a booking
/// ([BookingStatus]). Each mirrors a frozen `SIMF.Common.Enums` value and
/// decodes tolerantly (int OR name) per the append-only wire rule (D-219).
library;

/// How a reserved seat came to exist — mirrors
/// `SIMF.Common.Enums.SeatReservationKind` (frozen, int-backed: UserBooking=0,
/// AdminReservedRow=1, RandomAssignment=2). Int on the wire (no string-enum
/// converter); [fromJson] decodes tolerantly (int OR name; unknown →
/// [userBooking]).
enum SeatReservationKind {
  userBooking(0, 'UserBooking'),
  adminReservedRow(1, 'AdminReservedRow'),
  randomAssignment(2, 'RandomAssignment'),
  // D-485 — a general-admission join with no specific seat (null row/seat).
  openSeating(3, 'OpenSeating');

  const SeatReservationKind(this.wireValue, this.wireName);

  final int wireValue;
  final String wireName;

  /// An admin-blocked row is reserved but never a visitor's own seat.
  bool get isAdminBlock => this == SeatReservationKind.adminReservedRow;

  static SeatReservationKind fromJson(Object? value) {
    if (value is String) {
      for (final kind in values) {
        if (kind.wireName == value) {
          return kind;
        }
      }
    } else if (value is num) {
      final asInt = value.toInt();
      for (final kind in values) {
        if (kind.wireValue == asInt) {
          return kind;
        }
      }
    }
    return SeatReservationKind.userBooking;
  }
}

/// How attendees join a session — mirrors `SIMF.Common.Enums.SeatSelectionMode`
/// (int-backed: AssignedSeat=0, OpenSeating=1). Drives the session page's Join
/// CTA: an assigned-seat session opens the seat picker; an open-seating session
/// is a one-tap join. [fromJson] decodes tolerantly (int OR name; unknown →
/// [assignedSeat], so an older server that omits the field stays
/// seat-assigned).
enum SeatSelectionMode {
  assignedSeat(0, 'AssignedSeat'),
  openSeating(1, 'OpenSeating');

  const SeatSelectionMode(this.wireValue, this.wireName);

  final int wireValue;
  final String wireName;

  bool get isOpenSeating => this == SeatSelectionMode.openSeating;

  static SeatSelectionMode fromJson(Object? value) {
    if (value is String) {
      for (final mode in values) {
        if (mode.wireName == value) {
          return mode;
        }
      }
    } else if (value is num) {
      final asInt = value.toInt();
      for (final mode in values) {
        if (mode.wireValue == asInt) {
          return mode;
        }
      }
    }
    return SeatSelectionMode.assignedSeat;
  }
}

/// D-771 — the seat TIER of a hall-layout row, mirroring
/// `SIMF.Common.Enums.SeatTier` (int-backed: Normal=0, Vip=1, Vvip=2). The tier
/// is real data on the layout, not a label: it decides who may reserve the
/// seat. [fromJson] decodes tolerantly (int OR name; unknown → [normal], which
/// is also what a server that predates D-771 implies by omitting the key).
enum SeatTier {
  normal(0, 'Normal'),
  vip(1, 'Vip'),
  vvip(2, 'Vvip');

  const SeatTier(this.wireValue, this.wireName);

  final int wireValue;
  final String wireName;

  /// Protocol seating — nobody may self-reserve it; an administrator assigns it
  /// with a manual guest note.
  bool get isVvip => this == SeatTier.vvip;

  /// Whether a visitor may self-reserve a seat of this tier. Mirrors the
  /// server's single rule (`SeatReservationService.IsSelfReservable`) so the
  /// grid greys out exactly the seats the API would refuse — the server still
  /// re-checks.
  bool selfReservableBy({required bool callerIsVip}) => switch (this) {
        SeatTier.vvip => false,
        SeatTier.vip => callerIsVip,
        SeatTier.normal => true,
      };

  static SeatTier fromJson(Object? value) {
    if (value is String) {
      for (final tier in values) {
        if (tier.wireName == value) {
          return tier;
        }
      }
    } else if (value is num) {
      final asInt = value.toInt();
      for (final tier in values) {
        if (tier.wireValue == asInt) {
          return tier;
        }
      }
    }
    return SeatTier.normal;
  }
}

/// The booking-approval state of a reservation — mirrors
/// `SIMF.Common.Enums.BookingStatus` (int-backed: Pending=0, Approved=1,
/// Rejected=2, Cancelled=3). A fresh booking/join is [pending] until the Control
/// Panel approves it. [fromJson] tolerant (int OR name; unknown → [pending]).
enum BookingStatus {
  pending(0, 'Pending'),
  approved(1, 'Approved'),
  rejected(2, 'Rejected'),
  cancelled(3, 'Cancelled');

  const BookingStatus(this.wireValue, this.wireName);

  final int wireValue;
  final String wireName;

  static BookingStatus fromJson(Object? value) {
    if (value is String) {
      for (final status in values) {
        if (status.wireName == value) {
          return status;
        }
      }
    } else if (value is num) {
      final asInt = value.toInt();
      for (final status in values) {
        if (status.wireValue == asInt) {
          return status;
        }
      }
    }
    return BookingStatus.pending;
  }
}
