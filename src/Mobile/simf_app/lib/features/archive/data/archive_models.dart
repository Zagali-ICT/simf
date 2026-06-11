import 'package:flutter/foundation.dart';

/// One past edition — mirrors `PublicArchiveEdition` (`GET /app/archive`). Note
/// the wire names are `titleEn`/`titleAr` and `summaryEn`/`summaryAr`.
@immutable
class ArchiveEdition {
  const ArchiveEdition({
    required this.id,
    required this.year,
    required this.titleEn,
    required this.titleAr,
    required this.attendees,
    required this.sessions,
    required this.speakers,
    this.summaryEn,
    this.summaryAr,
    this.coverImageRelativePath,
  });

  final String id;
  final int year;
  final String titleEn;
  final String titleAr;
  final int attendees;
  final int sessions;
  final int speakers;
  final String? summaryEn;
  final String? summaryAr;
  final String? coverImageRelativePath;

  String localizedTitle(bool isArabic) => _pick(titleAr, titleEn, isArabic);
  String? localizedSummary(bool isArabic) =>
      _pickOpt(summaryAr, summaryEn, isArabic);

  static ArchiveEdition fromJson(Map<String, dynamic> json) => ArchiveEdition(
        id: json['id'] as String? ?? '',
        year: (json['year'] as num?)?.toInt() ?? 0,
        titleEn: json['titleEn'] as String? ?? '',
        titleAr: json['titleAr'] as String? ?? '',
        attendees: (json['attendees'] as num?)?.toInt() ?? 0,
        sessions: (json['sessions'] as num?)?.toInt() ?? 0,
        speakers: (json['speakers'] as num?)?.toInt() ?? 0,
        summaryEn: json['summaryEn'] as String?,
        summaryAr: json['summaryAr'] as String?,
        coverImageRelativePath: json['coverImageRelativePath'] as String?,
      );

  static List<ArchiveEdition> listFromData(Object? data) =>
      ((data is Map ? data['items'] : null) as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => ArchiveEdition.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);
}

/// The fuller edition detail — mirrors `PublicArchiveEditionDetail` (adds the
/// location + date-label pairs).
@immutable
class ArchiveEditionDetail {
  const ArchiveEditionDetail({
    required this.id,
    required this.year,
    required this.titleEn,
    required this.titleAr,
    required this.attendees,
    required this.sessions,
    required this.speakers,
    this.summaryEn,
    this.summaryAr,
    this.locationEn,
    this.locationAr,
    this.dateLabelEn,
    this.dateLabelAr,
  });

  final String id;
  final int year;
  final String titleEn;
  final String titleAr;
  final int attendees;
  final int sessions;
  final int speakers;
  final String? summaryEn;
  final String? summaryAr;
  final String? locationEn;
  final String? locationAr;
  final String? dateLabelEn;
  final String? dateLabelAr;

  String localizedTitle(bool isArabic) => _pick(titleAr, titleEn, isArabic);
  String? localizedSummary(bool isArabic) =>
      _pickOpt(summaryAr, summaryEn, isArabic);
  String? localizedLocation(bool isArabic) =>
      _pickOpt(locationAr, locationEn, isArabic);
  String? localizedDateLabel(bool isArabic) =>
      _pickOpt(dateLabelAr, dateLabelEn, isArabic);

  static ArchiveEditionDetail fromJson(Map<String, dynamic> json) =>
      ArchiveEditionDetail(
        id: json['id'] as String? ?? '',
        year: (json['year'] as num?)?.toInt() ?? 0,
        titleEn: json['titleEn'] as String? ?? '',
        titleAr: json['titleAr'] as String? ?? '',
        attendees: (json['attendees'] as num?)?.toInt() ?? 0,
        sessions: (json['sessions'] as num?)?.toInt() ?? 0,
        speakers: (json['speakers'] as num?)?.toInt() ?? 0,
        summaryEn: json['summaryEn'] as String?,
        summaryAr: json['summaryAr'] as String?,
        locationEn: json['locationEn'] as String?,
        locationAr: json['locationAr'] as String?,
        dateLabelEn: json['dateLabelEn'] as String?,
        dateLabelAr: json['dateLabelAr'] as String?,
      );
}

String _pick(String arabic, String english, bool isArabic) {
  final ar = arabic.trim();
  final en = english.trim();
  return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
}

String? _pickOpt(String? arabic, String? english, bool isArabic) {
  final ar = arabic?.trim() ?? '';
  final en = english?.trim() ?? '';
  final value = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  return value.isEmpty ? null : value;
}
