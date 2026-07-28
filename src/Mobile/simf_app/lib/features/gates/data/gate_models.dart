import 'package:flutter/foundation.dart';

/// Scan verdict — mirrors `ScanOutcome` (Allowed=0, Denied=1).
enum ScanOutcome { allowed, denied }

/// Direction the server resolved for the scan — mirrors `ScanDirection`
/// (CheckIn=0, CheckOut=1).
enum ScanDirection { checkIn, checkOut }

/// A gate's direction policy — mirrors `DirectionMode` (In=0, Out=1, Both=2).
/// A `both` gate lets the operator switch دخول/خروج on the console (D-509); a
/// fixed `inOnly` / `outOnly` gate locks the toggle to its one direction.
enum GateDirectionMode { inOnly, outOnly, both }

/// A gate the signed-in operator is assigned to — mirrors
/// `OperatorGateAssignment` (`GET /app/gates/my-assignments`).
@immutable
class OperatorGate {
  const OperatorGate({
    required this.gateId,
    required this.code,
    required this.name,
    required this.nameArabic,
    required this.directionMode,
    required this.isActive,
  });

  final String gateId;
  final String code;
  final String name;
  final String nameArabic;

  /// The gate's direction policy — drives whether the console's دخول/خروج
  /// toggle is operator-switchable (`both`) or locked to one direction (D-509).
  final GateDirectionMode directionMode;
  final bool isActive;

  String localizedName(bool isArabic) {
    final ar = nameArabic.trim();
    final en = name.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  static OperatorGate fromJson(Map<String, dynamic> json) => OperatorGate(
        gateId: json['gateId'] as String? ?? '',
        code: json['code'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        directionMode: _directionModeFromWire(json['directionMode']),
        isActive: json['isActive'] as bool? ?? false,
      );

  /// Decodes `DirectionMode` (In=0, Out=1, Both=2); anything else → `both`
  /// (the safe operator-switchable default).
  static GateDirectionMode _directionModeFromWire(Object? value) {
    switch ((value as num?)?.toInt()) {
      case 0:
        return GateDirectionMode.inOnly;
      case 1:
        return GateDirectionMode.outOnly;
      default:
        return GateDirectionMode.both;
    }
  }

  static List<OperatorGate> listFromData(Object? data) =>
      (data as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => OperatorGate.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);
}

/// The scanned holder's identity (when known) — mirrors `GateScanUserProfile`.
@immutable
class GateScanUserProfile {
  const GateScanUserProfile({
    required this.displayName,
    required this.displayNameArabic,
    this.profileTypeName,
  });

  final String displayName;
  final String displayNameArabic;
  final String? profileTypeName;

  String localizedName(bool isArabic) {
    final ar = displayNameArabic.trim();
    final en = displayName.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  static GateScanUserProfile? fromJson(Object? data) {
    if (data is! Map) {
      return null;
    }
    final json = data.cast<String, dynamic>();
    return GateScanUserProfile(
      displayName: json['displayName'] as String? ?? '',
      displayNameArabic: json['displayNameArabic'] as String? ?? '',
      profileTypeName: json['profileTypeName'] as String?,
    );
  }
}

/// The result of a gate scan — mirrors `GateScanResponse`. A **denial** is an
/// HTTP 200 with [outcome] == denied (only infra failures are non-200).
@immutable
class GateScanResult {
  const GateScanResult({
    required this.scanId,
    required this.outcome,
    required this.direction,
    this.userProfile,
    this.denialMessage,
    this.noticeMessage,
  });

  final int scanId;
  final ScanOutcome outcome;
  final ScanDirection direction;
  final GateScanUserProfile? userProfile;

  /// The server's already-localized denial text (the `DenialReasonCode` enum is
  /// not surfaced — the message is what the UI shows).
  final String? denialMessage;

  /// DEF-CHK-004 — the server's already-localized advisory note on a scan that
  /// was still ALLOWED (today: a hall-door scan with no session running, so no
  /// hall attendance was recorded). Null on an ordinary scan.
  final String? noticeMessage;

  bool get isAllowed => outcome == ScanOutcome.allowed;

  static GateScanResult fromJson(Map<String, dynamic> json) => GateScanResult(
        scanId: (json['scanId'] as num?)?.toInt() ?? 0,
        outcome: (json['outcome'] as num?)?.toInt() == 1
            ? ScanOutcome.denied
            : ScanOutcome.allowed,
        direction: (json['direction'] as num?)?.toInt() == 1
            ? ScanDirection.checkOut
            : ScanDirection.checkIn,
        userProfile: GateScanUserProfile.fromJson(json['userProfile']),
        denialMessage: json['denialMessage'] as String?,
        noticeMessage: json['noticeMessage'] as String?,
      );
}
