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

  factory ArchiveEdition.fromJson(Map<String, dynamic> json) => ArchiveEdition(
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

  String localizedTitle({required bool isArabic}) =>
      _pick(titleAr, titleEn, isArabic);
  String? localizedSummary({required bool isArabic}) =>
      _pickOpt(summaryAr, summaryEn, isArabic);

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
    this.gallery = const <ArchiveMediaItem>[],
    this.sessionTitles = const <ArchiveSessionTitle>[],
    this.pastSpeakers = const <ArchivePastSpeaker>[],
  });

  factory ArchiveEditionDetail.fromJson(Map<String, dynamic> json) =>
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
        gallery: _list(json['gallery'], ArchiveMediaItem.fromJson),
        sessionTitles:
            _list(json['sessionTitles'], ArchiveSessionTitle.fromJson),
        pastSpeakers: _list(json['pastSpeakers'], ArchivePastSpeaker.fromJson),
      );

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
  // D-432 — the rich per-edition lists (Mockup 24-01).
  final List<ArchiveMediaItem> gallery;
  final List<ArchiveSessionTitle> sessionTitles;
  final List<ArchivePastSpeaker> pastSpeakers;

  String localizedTitle({required bool isArabic}) =>
      _pick(titleAr, titleEn, isArabic);
  String? localizedSummary({required bool isArabic}) =>
      _pickOpt(summaryAr, summaryEn, isArabic);
  String? localizedLocation({required bool isArabic}) =>
      _pickOpt(locationAr, locationEn, isArabic);
  String? localizedDateLabel({required bool isArabic}) =>
      _pickOpt(dateLabelAr, dateLabelEn, isArabic);

  static List<T> _list<T>(
    Object? raw,
    T Function(Map<String, dynamic>) fromJson,
  ) =>
      (raw as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);
}

/// One gallery item — mirrors `PublicArchiveMediaItem`. `kind` is 0 image / 1
/// video.
@immutable
class ArchiveMediaItem {
  const ArchiveMediaItem({
    required this.kind,
    required this.url,
    this.captionEn,
    this.captionAr,
  });

  factory ArchiveMediaItem.fromJson(Map<String, dynamic> json) =>
      ArchiveMediaItem(
        kind: (json['kind'] as num?)?.toInt() ?? 0,
        url: json['url'] as String? ?? '',
        captionEn: json['captionEn'] as String?,
        captionAr: json['captionAr'] as String?,
      );

  final int kind;
  final String url;
  final String? captionEn;
  final String? captionAr;

  bool get isVideo => kind == 1;
  String? localizedCaption({required bool isArabic}) =>
      _pickOpt(captionAr, captionEn, isArabic);
}

/// One past-edition session title — mirrors `PublicArchiveSessionTitle`.
@immutable
class ArchiveSessionTitle {
  const ArchiveSessionTitle({required this.titleEn, required this.titleAr});

  factory ArchiveSessionTitle.fromJson(Map<String, dynamic> json) =>
      ArchiveSessionTitle(
        titleEn: json['titleEn'] as String? ?? '',
        titleAr: json['titleAr'] as String? ?? '',
      );

  final String titleEn;
  final String titleAr;

  String localized({required bool isArabic}) =>
      _pick(titleAr, titleEn, isArabic);
}

/// One past speaker — mirrors `PublicArchivePastSpeaker`.
@immutable
class ArchivePastSpeaker {
  const ArchivePastSpeaker({
    required this.nameEn,
    required this.nameAr,
    this.photoRelativePath,
    this.countryId,
  });

  factory ArchivePastSpeaker.fromJson(Map<String, dynamic> json) =>
      ArchivePastSpeaker(
        nameEn: json['nameEn'] as String? ?? '',
        nameAr: json['nameAr'] as String? ?? '',
        photoRelativePath: json['photoRelativePath'] as String?,
        countryId: (json['countryId'] as num?)?.toInt(),
      );

  final String nameEn;
  final String nameAr;
  final String? photoRelativePath;
  // D-456 — optional country (ISO 3166-1 numeric) for the corner flag.
  final int? countryId;

  String localized({required bool isArabic}) => _pick(nameAr, nameEn, isArabic);
}

String _pick(String arabic, String english, bool isArabic) {
  final ar = arabic.trim();
  final en = english.trim();
  return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
}

String? _pickOpt(String? arabic, String? english, bool isArabic) {
  final ar = arabic?.trim() ?? '';
  final en = english?.trim() ?? '';
  final value =
      isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  return value.isEmpty ? null : value;
}
