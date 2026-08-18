import 'package:flutter/foundation.dart';
import 'package:simf_app/features/sessions/data/seat_enums.dart';

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

  factory MyReservation.fromJson(Map<String, dynamic> json) => MyReservation(
        reservationId: json['reservationId'] as String? ?? '',
        sessionId: json['sessionId'] as String? ?? '',
        rowLabel: json['rowLabel'] as String?,
        seatNumber: (json['seatNumber'] as num?)?.toInt(),
        kind: SeatReservationKind.fromJson(json['kind']),
        status: BookingStatus.fromJson(json['status']),
      );

  final String reservationId;
  final String sessionId;
  final String? rowLabel;
  final int? seatNumber;
  final SeatReservationKind kind;
  final BookingStatus status;

  bool get isOpenSeating => kind == SeatReservationKind.openSeating;
}
