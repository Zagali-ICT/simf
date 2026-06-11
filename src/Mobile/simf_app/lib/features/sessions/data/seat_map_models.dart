import 'package:flutter/foundation.dart';

/// How a reserved seat came to exist — mirrors
/// `SIMF.Common.Enums.SeatReservationKind` (frozen, int-backed: UserBooking=0,
/// AdminReservedRow=1, RandomAssignment=2). Int on the wire (no string-enum
/// converter); [fromJson] decodes tolerantly (int OR name; unknown →
/// [userBooking]).
enum SeatReservationKind {
  userBooking(0, 'UserBooking'),
  adminReservedRow(1, 'AdminReservedRow'),
  randomAssignment(2, 'RandomAssignment');

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
  });

  final String? reservationId;
  final String rowLabel;
  final int seatNumber;
  final SeatReservationKind kind;

  /// A stable `row:seat` key for set membership (status derivation, L-2).
  String get key => '$rowLabel:$seatNumber';

  static SeatCell fromJson(Map<String, dynamic> json) => SeatCell(
        reservationId: json['reservationId'] as String?,
        rowLabel: json['rowLabel'] as String? ?? '',
        seatNumber: (json['seatNumber'] as num?)?.toInt() ?? 0,
        kind: SeatReservationKind.fromJson(json['kind']),
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
  });

  final List<String> rowLabels;
  final int seatsPerRow;
  final List<SeatCell> reservedCells;
  final SeatCell? myCell;
  final int activeReservedCount;
  final int hallCapacity;
  final int? sessionCapacity;

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
    );
  }
}
