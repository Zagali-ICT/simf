import 'package:flutter/foundation.dart';
import 'package:simf_app/core/utils/bilingual.dart';
import 'package:simf_app/features/sessions/data/session_enums.dart';

/// One speaker card carried on a cached session — mirrors
/// `SIMF.Contracts.Programme.PublicSessionSpeaker`. The country **flag** is
/// rendered from [countryId] (the names are the label/fallback) and the
/// **avatar** from [photoRelativePath]; all four are nullable + append-only
/// (D-271). Decoded here so the cached programme feeds the session detail
/// (Page_017) with no extra fetch — the Sessions **list** row itself does not
/// render speakers (the mockup row is time/index/title/description, Page_016
/// Design).
@immutable
class SessionSpeaker {
  const SessionSpeaker({
    required this.id,
    required this.name,
    required this.nameArabic,
    required this.displayOrder,
    required this.role,
    this.title,
    this.titleArabic,
    this.countryId,
    this.countryNameEn,
    this.countryNameAr,
    this.photoRelativePath,
  });

  factory SessionSpeaker.fromJson(Map<String, dynamic> json) => SessionSpeaker(
        id: json['id'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        title: json['title'] as String?,
        titleArabic: json['titleArabic'] as String?,
        displayOrder: (json['displayOrder'] as num?)?.toInt() ?? 0,
        role: SessionSpeakerRole.fromJson(json['role']),
        countryId: (json['countryId'] as num?)?.toInt(),
        countryNameEn: json['countryNameEn'] as String?,
        countryNameAr: json['countryNameAr'] as String?,
        photoRelativePath: json['photoRelativePath'] as String?,
      );

  final String id;
  final String name;
  final String nameArabic;
  final String? title;
  final String? titleArabic;
  final int displayOrder;
  final SessionSpeakerRole role;
  final int? countryId;
  final String? countryNameEn;
  final String? countryNameAr;
  final String? photoRelativePath;

  String localizedName({required bool isArabic}) =>
      pickLocalized(nameArabic, name, isArabic: isArabic);

  // 2026-07-19 (owner) — the speaker's rank/title in the active locale
  // (Arabic ↔ English), matching how the name localizes. Null when unset.
  String? localizedTitle({required bool isArabic}) =>
      pickLocalizedOrNull(titleArabic, title, isArabic: isArabic);

  String? localizedCountry({required bool isArabic}) =>
      pickLocalizedOrNull(countryNameAr, countryNameEn, isArabic: isArabic);
}
