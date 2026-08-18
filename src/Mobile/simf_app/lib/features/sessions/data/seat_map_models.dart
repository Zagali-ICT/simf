import 'package:flutter/foundation.dart';
import 'package:simf_app/features/sessions/data/seat_enums.dart';

/// One occupied (or own) seat — mirrors
/// `SIMF.Contracts.Sessions.SessionSeatCell`. The location is [rowLabel] +
/// [seatNumber] (1-based within the row); there is **no column axis** (Page_018
/// L-3).
@immutable
class SeatCell {
  const SeatCell({
    required this.rowLabel,
    required this.seatNumber,
    required this.kind,
    this.reservationId,
    this.status = BookingStatus.pending,
    this.checkedIn = false,
    this.guestHint,
    this.guestHintArabic,
  });

  factory SeatCell.fromJson(Map<String, dynamic> json) => SeatCell(
        reservationId: json['reservationId'] as String?,
        rowLabel: json['rowLabel'] as String? ?? '',
        seatNumber: (json['seatNumber'] as num?)?.toInt() ?? 0,
        kind: SeatReservationKind.fromJson(json['kind']),
        status: BookingStatus.fromJson(json['status']),
        checkedIn: json['checkedIn'] as bool? ?? false,
        guestHint: json['guestHint'] as String?,
        guestHintArabic: json['guestHintArabic'] as String?,
      );

  final String? reservationId;
  final String rowLabel;
  final int seatNumber;
  final SeatReservationKind kind;

  /// D-771 — the administrator's manual guest note on a VVIP seat (append-only
  /// wire keys `guestHint` / `guestHintArabic`). A VVIP seat has no
  /// registration, so this text is the occupant record the seat tooltip shows.
  /// Null everywhere else.
  final String? guestHint;
  final String? guestHintArabic;

  /// The locale-appropriate guest note, falling back to the other language,
  /// then null when the admin typed neither.
  String? localizedGuestHint({required bool isArabic}) {
    final ar = (guestHintArabic ?? '').trim();
    final en = (guestHint ?? '').trim();
    final primary = isArabic ? ar : en;
    if (primary.isNotEmpty) {
      return primary;
    }
    final fallback = isArabic ? en : ar;
    return fallback.isEmpty ? null : fallback;
  }

  /// D-572 — the booking's approval state (append-only wire key `status`),
  /// used by the "my seat" card to switch its hint. Defaults to `pending` so an
  /// older server that omits the field reads as awaiting approval.
  final BookingStatus status;

  /// A12 — the holder has an OPEN hall-attendance row for this session: they
  /// scanned in at the gate, so the seat is **confirmed** (تم التأكيد)
  /// rather than merely held. Wire key `checkedIn`, shipped since Wave 2 but
  /// never decoded, which is why the fourth seat state could not render.
  /// Defaults to false, so a server that omits it reads as "not yet arrived".
  final bool checkedIn;

  /// A stable `row:seat` key for set membership (status derivation, L-2).
  String get key => '$rowLabel:$seatNumber';
}

/// The full hall seat grid for one session — mirrors
/// `SIMF.Contracts.Sessions.SessionSeatMap` (`GET /app/sessions/{id}/seats`,
/// approved account). One read draws the whole grid: [rowLabels] rows, the
/// [reservedCells] occupied, [myCell] the caller's own seat (Page_018 L-1).
/// Seat status is **derived** client-side (L-2): mine = [myCell]; reserved =
/// in [reservedCells]; available = neither.
///
/// Row width is [seatsPerRow] when uniform, or — when the layout gives each row
/// its own width — [seatCounts] (a per-row count PARALLEL to [rowLabels],
/// appended on the wire as `seatCounts`; the shipped `seatsPerRow` key is kept
/// as the uniform fallback). Read a row's width through [seatsInRow], which
/// prefers [seatCounts] only when its length matches [rowLabels] and otherwise
/// falls back to [seatsPerRow].
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
    this.seatCounts = const <int>[],
    this.seatTiers = const <SeatTier>[],
    this.callerIsVip = false,
  });

  factory SessionSeatMap.fromJson(Map<String, dynamic> json) {
    final myCellJson = json['myCell'];
    return SessionSeatMap(
      rowLabels: (json['rowLabels'] as List? ?? const <dynamic>[])
          .whereType<String>()
          .toList(growable: false),
      seatsPerRow: (json['seatsPerRow'] as num?)?.toInt() ?? 0,
      seatCounts: (json['seatCounts'] as List? ?? const <dynamic>[])
          .whereType<num>()
          .map((n) => n.toInt())
          .toList(growable: false),
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
      seatTiers: (json['seatTiers'] as List? ?? const <dynamic>[])
          .map(SeatTier.fromJson)
          .toList(growable: false),
      callerIsVip: json['callerIsVip'] as bool? ?? false,
    );
  }

  final List<String> rowLabels;
  final int seatsPerRow;
  // Per-row seat counts PARALLEL to [rowLabels] (append-only wire key
  // `seatCounts`). Empty (or length-mismatched) → the grid stays uniform via
  // [seatsPerRow]; see [seatsInRow].
  final List<int> seatCounts;

  /// D-771 — per-row seat TIERS PARALLEL to [rowLabels] (append-only wire key
  /// `seatTiers`). Empty (or length-mismatched) → every row reads as
  /// [SeatTier.normal], which is exactly what a pre-D-771 server implies. Read
  /// a row's tier through [tierOfRow].
  final List<SeatTier> seatTiers;

  /// D-771 — whether the SIGNED-IN caller is a VIP-tier visitor (append-only
  /// wire key `callerIsVip`). Drives which rows the picker offers; the server
  /// re-checks on every reserve, so this is a UX hint, never the gate.
  final bool callerIsVip;

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

  String? localizedSessionTitle({required bool isArabic}) {
    final ar = (sessionTitleArabic ?? '').trim();
    final en = (sessionTitle ?? '').trim();
    final primary = isArabic ? ar : en;
    if (primary.isNotEmpty) {
      return primary;
    }
    final fallback = isArabic ? en : ar;
    return fallback.isEmpty ? null : fallback;
  }

  /// The seat count of row [i] (0-based). Prefers [seatCounts] only when its
  /// length matches [rowLabels]; otherwise (absent or length-mismatched) falls
  /// back to the uniform [seatsPerRow].
  int seatsInRow(int i) {
    if (seatCounts.length == rowLabels.length &&
        i >= 0 &&
        i < seatCounts.length) {
      return seatCounts[i];
    }
    return seatsPerRow;
  }

  /// D-771 — the tier of row [i] (0-based). Prefers [seatTiers] only when its
  /// length matches [rowLabels]; otherwise (absent or length-mismatched) every
  /// row reads [SeatTier.normal], the pre-D-771 behaviour.
  SeatTier tierOfRow(int i) {
    if (seatTiers.length == rowLabels.length &&
        i >= 0 &&
        i < seatTiers.length) {
      return seatTiers[i];
    }
    return SeatTier.normal;
  }

  /// D-771 — whether THIS caller may self-reserve a seat in row [i]. Mirrors
  /// the server rule so the grid pre-disables exactly what the API would
  /// refuse.
  bool canReserveRow(int i) =>
      tierOfRow(i).selfReservableBy(callerIsVip: callerIsVip);

  /// True when at least one row carries a tier above Normal — the picker only
  /// shows the tier legend/explanation for a tiered hall.
  bool get hasTiers {
    for (var i = 0; i < rowLabels.length; i++) {
      if (tierOfRow(i) != SeatTier.normal) {
        return true;
      }
    }
    return false;
  }

  /// The widest row's seat count — the number of seat COLUMNS the grid sizes to
  /// so every row draws identically-sized squares. Plain loop (no `dart:math`).
  int get maxSeatsPerRow {
    var result = seatsPerRow;
    for (var i = 0; i < rowLabels.length; i++) {
      final count = seatsInRow(i);
      if (count > result) {
        result = count;
      }
    }
    return result;
  }

  /// False when the hall has no drawable layout (L-6). Defined via
  /// [maxSeatsPerRow] so it stays consistent with the grid's column count: a
  /// variable layout with a zero uniform [seatsPerRow] still draws while its
  /// (length-matched) [seatCounts] carry a positive count, but a degraded
  /// response whose [seatCounts] length does not match [rowLabels] collapses to
  /// the uniform fallback and reports no layout — the safe empty state, never a
  /// zero-column grid.
  bool get hasLayout => rowLabels.isNotEmpty && maxSeatsPerRow > 0;

  int get capacity => sessionCapacity ?? hallCapacity;

  /// The `row:seat` keys of every occupied seat — built once for O(1) lookup.
  Set<String> reservedKeys() =>
      <String>{for (final cell in reservedCells) cell.key};

  /// A12 — every occupied seat by its `row:seat` key, so the grid can read
  /// the CELL (and therefore [SeatCell.checkedIn]) and not just "is it
  /// taken". Built once per render, like [reservedKeys].
  Map<String, SeatCell> reservedByKey() =>
      <String, SeatCell>{for (final cell in reservedCells) cell.key: cell};

  /// A12 — true when at least one seat is confirmed (its holder checked in).
  /// The confirmed legend entry only appears for a hall that actually has
  /// one, mirroring how [hasTiers] gates the tier legend.
  bool get hasConfirmed {
    for (final cell in reservedCells) {
      if (cell.checkedIn) {
        return true;
      }
    }
    return false;
  }

  bool isMine(String rowLabel, int seatNumber) =>
      myCell != null &&
      myCell!.rowLabel == rowLabel &&
      myCell!.seatNumber == seatNumber;
}
