/// Wire models for the staff seating desk (D-771).
///
/// Split out of the repository file: a repository is the
/// transport, and the DTO is the contract. JSON keys are the
/// shipped wire contract (D-219) and are unchanged by the move.
library;

import 'package:flutter/foundation.dart';

import 'package:simf_app/features/sessions/data/seat_map_models.dart';

/// D-771 (owner 2026-07-26) — one resolved seat occupant, mirroring
/// `SIMF.Contracts.Sessions.StaffSeatOccupant`. The staff seating desk renders a
/// single result card from this shape whether the lookup started from a scanned
/// badge or from a tapped seat.
@immutable
class StaffSeatOccupant {
  const StaffSeatOccupant({
    required this.found,
    required this.tier,
    required this.kind,
    required this.status,
    required this.displayName,
    required this.displayNameArabic,
    required this.hasPhoto,
    required this.checkedIn,
    this.rowLabel,
    this.seatNumber,
    this.reservationId,
    this.userId,
    this.guestHint,
    this.guestHintArabic,
    this.qrId,
  });

  factory StaffSeatOccupant.fromJson(Map<String, dynamic> json) =>
      StaffSeatOccupant(
        found: json['found'] as bool? ?? false,
        rowLabel: json['rowLabel'] as String?,
        seatNumber: (json['seatNumber'] as num?)?.toInt(),
        tier: SeatTier.fromJson(json['tier']),
        reservationId: json['reservationId'] as String?,
        kind: SeatReservationKind.fromJson(json['kind']),
        status: BookingStatus.fromJson(json['status']),
        userId: json['userId'] as String?,
        displayName: json['displayName'] as String? ?? '',
        displayNameArabic: json['displayNameArabic'] as String? ?? '',
        guestHint: json['guestHint'] as String?,
        guestHintArabic: json['guestHintArabic'] as String?,
        hasPhoto: json['hasPhoto'] as bool? ?? false,
        qrId: json['qrId'] as String?,
        checkedIn: json['checkedIn'] as bool? ?? false,
      );

  /// False when the lookup found nothing: an empty seat, or a valid badge that
  /// holds no seat in this session. Never an error state — the desk shows the
  /// matching "no seat" / "seat empty" message.
  final bool found;
  final String? rowLabel;
  final int? seatNumber;
  final SeatTier tier;
  final String? reservationId;
  final SeatReservationKind kind;
  final BookingStatus status;
  final String? userId;
  final String displayName;
  final String displayNameArabic;

  /// The administrator's manual note on a VVIP seat — the occupant record for a
  /// seat that has no registration.
  final String? guestHint;
  final String? guestHintArabic;

  /// True when the guest's photo can be streamed. It MUST be fetched through the
  /// authenticated bytes path (D-422) — a raw `Image.network` cannot carry the
  /// bearer token.
  final bool hasPhoto;
  final String? qrId;
  final bool checkedIn;

  /// The locale-appropriate name, falling back to the other language.
  String localizedName({required bool isArabic}) {
    final ar = displayNameArabic.trim();
    final en = displayName.trim();
    final primary = isArabic ? ar : en;
    return primary.isNotEmpty ? primary : (isArabic ? en : ar);
  }

  /// The locale-appropriate VVIP guest note (null when the admin typed neither).
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
}
