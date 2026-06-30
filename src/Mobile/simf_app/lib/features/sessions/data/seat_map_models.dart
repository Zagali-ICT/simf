import 'package:flutter/foundation.dart';

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
/// [assignedSeat], so an older server that omits the field stays seat-assigned).
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

/// The result of a reserve / random-allocate / open-seating join — mirrors
/// `SIMF.Contracts.Sessions.MySeatReservation`. [rowLabel]/[seatNumber] are null
/// for an [SeatReservationKind.openSeating] join (general admission — no seat).
@immutable
class MyReservation {
  const MyReservation({
    required this.reservationId,
    required this.sessionId,
    required this.kind,
    required this.status,
    this.rowLabel,
    this.seatNumber,
  });

  final String reservationId;
  final String sessionId;
  final String? rowLabel;
  final int? seatNumber;
  final SeatReservationKind kind;
  final BookingStatus status;

  /// True for a general-admission join (no specific seat).
  bool get isOpenSeating => kind == SeatReservationKind.openSeating;

  static MyReservation fromJson(Map<String, dynamic> json) => MyReservation(
        reservationId: json['reservationId'] as String? ?? '',
        sessionId: json['sessionId'] as String? ?? '',
        rowLabel: json['rowLabel'] as String?,
        seatNumber: (json['seatNumber'] as num?)?.toInt(),
        kind: SeatReservationKind.fromJson(json['kind']),
        status: BookingStatus.fromJson(json['status']),
      );
}

/// One occupied (or own) seat — mirrors `SIMF.Contracts.Sessions.SessionSeatCell`.
/// The location is [rowLabel] + [seatNumber] (1-based within the row); there is
/// **no column axis** (Page_018 L-3).
@immutable
class SeatCell {
  const SeatCell({
    required this.rowLabel,
    required this.seatNumber,
    required this.kind,
    this.reservationId,
    this.status = BookingStatus.pending,
  });

  final String? reservationId;
  final String rowLabel;
  final int seatNumber;
  final SeatReservationKind kind;

  /// D-572 — the booking's approval state (append-only wire key `status`),
  /// used by the "my seat" card to switch its hint. Defaults to [pending] so an
  /// older server that omits the field reads as awaiting approval.
  final BookingStatus status;

  /// A stable `row:seat` key for set membership (status derivation, L-2).
  String get key => '$rowLabel:$seatNumber';

  static SeatCell fromJson(Map<String, dynamic> json) => SeatCell(
        reservationId: json['reservationId'] as String?,
        rowLabel: json['rowLabel'] as String? ?? '',
        seatNumber: (json['seatNumber'] as num?)?.toInt() ?? 0,
        kind: SeatReservationKind.fromJson(json['kind']),
        status: BookingStatus.fromJson(json['status']),
      );
}

/// The full hall seat grid for one session — mirrors
/// `SIMF.Contracts.Sessions.SessionSeatMap` (`GET /app/sessions/{id}/seats`,
/// approved account). One read draws the whole grid: [rowLabels] × [seatsPerRow]
/// cells, the [reservedCells] occupied, [myCell] the caller's own seat (Page_018
/// L-1). Seat status is **derived** client-side (L-2): mine = [myCell]; reserved =
/// in [reservedCells]; available = neither.
@immutable
class SessionSeatMap {
  const SessionSeatMap({
    required this.rowLabels,
    required this.seatsPerRow,
    required this.reservedCells,
    required this.activeReservedCount,
    required this.hallCapacity,
    this.myCell,
    this.sessionCapacity,
    this.sessionTitle,
    this.sessionTitleArabic,
    this.mode = SeatSelectionMode.assignedSeat,
  });

  final List<String> rowLabels;
  final int seatsPerRow;
  final List<SeatCell> reservedCells;
  final SeatCell? myCell;
  final int activeReservedCount;
  final int hallCapacity;
  final int? sessionCapacity;
  // D-432 — the session's bilingual title now ships on the seat-map response
  // (no second /sessions/{id} call needed for the "my seat" header).
  final String? sessionTitle;
  final String? sessionTitleArabic;
  // D-485 — the session's effective seat-selection mode (Session override, else
  // Hall default). The session page branches its Join CTA on this.
  final SeatSelectionMode mode;

  /// The locale-appropriate session title (null when neither is present).
  String? localizedSessionTitle(bool isArabic) {
    final ar = (sessionTitleArabic ?? '').trim();
    final en = (sessionTitle ?? '').trim();
    final primary = isArabic ? ar : en;
    if (primary.isNotEmpty) {
      return primary;
    }
    final fallback = isArabic ? en : ar;
    return fallback.isEmpty ? null : fallback;
  }

  /// False when the hall has no configured layout (L-6) — the grid can't draw.
  bool get hasLayout => rowLabels.isNotEmpty && seatsPerRow > 0;

  /// The effective capacity readout (session override, else hall).
  int get capacity => sessionCapacity ?? hallCapacity;

  /// The `row:seat` keys of every occupied seat — built once for O(1) lookup.
  Set<String> reservedKeys() =>
      <String>{for (final cell in reservedCells) cell.key};

  bool isMine(String rowLabel, int seatNumber) =>
      myCell != null &&
      myCell!.rowLabel == rowLabel &&
      myCell!.seatNumber == seatNumber;

  static SessionSeatMap fromJson(Map<String, dynamic> json) {
    final myCellJson = json['myCell'];
    return SessionSeatMap(
      rowLabels: (json['rowLabels'] as List? ?? const <dynamic>[])
          .whereType<String>()
          .toList(growable: false),
      seatsPerRow: (json['seatsPerRow'] as num?)?.toInt() ?? 0,
      reservedCells: (json['reservedCells'] as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => SeatCell.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false),
      myCell: myCellJson is Map
          ? SeatCell.fromJson(myCellJson.cast<String, dynamic>())
          : null,
      activeReservedCount: (json['activeReservedCount'] as num?)?.toInt() ?? 0,
      hallCapacity: (json['hallCapacity'] as num?)?.toInt() ?? 0,
      sessionCapacity: (json['sessionCapacity'] as num?)?.toInt(),
      sessionTitle: json['sessionTitle'] as String?,
      sessionTitleArabic: json['sessionTitleArabic'] as String?,
      mode: SeatSelectionMode.fromJson(json['mode']),
    );
  }
}
