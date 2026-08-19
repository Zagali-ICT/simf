import 'package:flutter/foundation.dart';
import 'package:simf_app/core/utils/bilingual.dart';
import 'package:simf_app/core/utils/saudi_time.dart';

/// Visitor-to-visitor contact sharing (SIMF-FDS-014 §5.4–5.7, D-286). All three
/// records mirror `SIMF.Contracts.Contacts.*` and decode tolerantly: the wire
/// fields are camelCase and **enum-free** (no int/name decode trap here), and a
/// missing field falls back to a safe blank so an older/newer payload never
/// crashes the screen (D-219, append-only).

/// The caller's own share token (`GET /app/account/share-token`). The mobile
/// client renders [token] as a QR and as the seed of an OS share-intent vCard.
@immutable
class VisitorShareToken {
  const VisitorShareToken(this.token);

  factory VisitorShareToken.fromData(Object? data) => VisitorShareToken(
        (data is Map ? data['token'] as String? : null) ?? '',
      );

  final String token;
}

/// A visitor's contact card — projected **live** from their `UserProfile`
/// (+ Organisation / Country lookups + a permitted email round-trip). No photo
/// in V1. When the subject is unavailable (deactivated / no profile),
/// [available] is false and the name fields are blank (E2E-MMC-011).
@immutable
class VisitorCard {
  const VisitorCard({
    required this.userId,
    required this.name,
    required this.nameArabic,
    required this.available,
    this.jobTitle,
    this.jobTitleArabic,
    this.organisation,
    this.organisationArabic,
    this.email,
    this.saudiMobile,
    this.internationalMobile,
    this.countryId,
    this.countryName,
    this.countryNameArabic,
  });

  factory VisitorCard.fromData(Object? data) {
    final json =
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
    return VisitorCard(
      userId: json['userId'] as String? ?? '',
      name: json['name'] as String? ?? '',
      nameArabic: json['nameArabic'] as String? ?? '',
      available: json['available'] as bool? ?? false,
      jobTitle: json['jobTitle'] as String?,
      jobTitleArabic: json['jobTitleArabic'] as String?,
      organisation: json['organisation'] as String?,
      organisationArabic: json['organisationArabic'] as String?,
      email: json['email'] as String?,
      saudiMobile: json['saudiMobile'] as String?,
      internationalMobile: json['internationalMobile'] as String?,
      countryId: (json['countryId'] as num?)?.toInt(),
      countryName: json['countryName'] as String?,
      countryNameArabic: json['countryNameArabic'] as String?,
    );
  }

  final String userId;
  final String name;
  final String nameArabic;
  final bool available;
  final String? jobTitle;
  final String? jobTitleArabic;
  final String? organisation;
  final String? organisationArabic;
  final String? email;
  final String? saudiMobile;
  final String? internationalMobile;
  final int? countryId;
  final String? countryName;
  final String? countryNameArabic;

  /// Name for the active locale (Arabic primary, English fallback).
  String localizedName({required bool isArabic}) =>
      pickLocalized(nameArabic, name, isArabic: isArabic);

  String? localizedJobTitle({required bool isArabic}) =>
      pickLocalizedOrNull(jobTitleArabic, jobTitle, isArabic: isArabic);

  String? localizedOrganisation({required bool isArabic}) =>
      pickLocalizedOrNull(organisationArabic, organisation, isArabic: isArabic);

  String? localizedCountry({required bool isArabic}) =>
      pickLocalizedOrNull(countryNameArabic, countryName, isArabic: isArabic);
}

/// One row in the caller's *My Contacts* list — resolved on read from the
/// subject's profile (no stored PII snapshot, D-157). [organisation] is a
/// single field here (the saved-row projection does not carry the Arabic
/// variant).
@immutable
class SavedContactRow {
  const SavedContactRow({
    required this.id,
    required this.subjectUserId,
    required this.name,
    required this.nameArabic,
    required this.subjectAvailable,
    this.jobTitle,
    this.jobTitleArabic,
    this.organisation,
    this.note,
    this.savedAt,
  });

  factory SavedContactRow.fromData(Object? data) {
    final json =
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
    return SavedContactRow(
      id: json['id'] as String? ?? '',
      subjectUserId: json['subjectUserId'] as String? ?? '',
      name: json['name'] as String? ?? '',
      nameArabic: json['nameArabic'] as String? ?? '',
      subjectAvailable: json['subjectAvailable'] as bool? ?? false,
      jobTitle: json['jobTitle'] as String?,
      jobTitleArabic: json['jobTitleArabic'] as String?,
      organisation: json['organisation'] as String?,
      note: json['note'] as String?,
      savedAt: parseWireOrNull(json['savedAt'] as String? ?? ''),
    );
  }

  final String id;
  final String subjectUserId;
  final String name;
  final String nameArabic;
  final bool subjectAvailable;
  final String? jobTitle;
  final String? jobTitleArabic;
  final String? organisation;
  final String? note;
  final DateTime? savedAt;

  String localizedName({required bool isArabic}) =>
      pickLocalized(nameArabic, name, isArabic: isArabic);

  String? localizedJobTitle({required bool isArabic}) =>
      pickLocalizedOrNull(jobTitleArabic, jobTitle, isArabic: isArabic);

  /// Reads the My-Contacts payload (a bare list, or `{ items: [...] }`).
  static List<SavedContactRow> listFromData(Object? data) {
    final list = data is Map ? data['items'] : data;
    return (list as List? ?? const <dynamic>[])
        .whereType<Map<dynamic, dynamic>>()
        .map((e) => SavedContactRow.fromData(e.cast<String, dynamic>()))
        .toList(growable: false);
  }
}
