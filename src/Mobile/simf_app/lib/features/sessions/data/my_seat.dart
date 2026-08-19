import 'package:flutter/foundation.dart';

/// The caller's own active seat for a session — the `myCell` cell of
/// `SIMF.Contracts.Sessions.SessionSeatMap` (`GET /app/sessions/{id}/seats`,
/// approved account). The Page_017 card shows `الصف {rowLabel} · مقعد
/// {seatNumber}`
/// — there is **no column axis** (Page_017 L-3.1). The full grid + the cell
/// `kind`
/// belong to the My-Seat screen (18).
@immutable
class MySeat {
  const MySeat({
    required this.reservationId,
    required this.rowLabel,
    required this.seatNumber,
  });

  final String reservationId;
  final String rowLabel;
  final int seatNumber;

  /// Reads `myCell` from a `SessionSeatMap` payload; null when the caller has
  /// no
  /// active reservation (the card is hidden — Page_017 L-3).
  static MySeat? fromSeatMap(Object? data) {
    final cell = (data is Map ? data['myCell'] : null);
    if (cell is! Map) {
      return null;
    }
    final map = cell.cast<String, dynamic>();
    return MySeat(
      reservationId: map['reservationId'] as String? ?? '',
      rowLabel: map['rowLabel'] as String? ?? '',
      seatNumber: (map['seatNumber'] as num?)?.toInt() ?? 0,
    );
  }
}
