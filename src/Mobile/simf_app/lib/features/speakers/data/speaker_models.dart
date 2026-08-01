import 'package:flutter/foundation.dart';

import '../../../core/country_flag.dart';
import '../../../core/utils/saudi_time.dart';

/// One row in the public speakers list — mirrors
/// `SIMF.Contracts.Programme.PublicSpeakerSummary` (`GET /app/speakers`). The
/// card shows the avatar (from [photoRelativePath]), the bilingual name, the
/// rank
/// line and the country (flag from [countryId] — interim renders the name).
@immutable
class SpeakerSummary {
  const SpeakerSummary({
    required this.id,
    required this.name,
    required this.nameArabic,
    required this.displayOrder,
    this.rank,
    this.rankArabic,
    this.countryId,
    this.countryNameEn,
    this.countryNameAr,
    this.photoRelativePath,
  });

  final String id;
  final String name;
  final String nameArabic;
  final int displayOrder;
  final String? rank;
  final String? rankArabic;
  final int? countryId;
  final String? countryNameEn;
  final String? countryNameAr;
  final String? photoRelativePath;

  String localizedName(bool isArabic) => _pick(nameArabic, name, isArabic);
  String? localizedRank(bool isArabic) =>
      _pickOpt(rankArabic, rank, isArabic);
  String? localizedCountry(bool isArabic) =>
      _pickOpt(countryNameAr, countryNameEn, isArabic);

  static SpeakerSummary fromJson(Map<String, dynamic> json) => SpeakerSummary(
        id: json['id'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        displayOrder: (json['displayOrder'] as num?)?.toInt() ?? 0,
        rank: json['rank'] as String?,
        rankArabic: json['rankArabic'] as String?,
        countryId: (json['countryId'] as num?)?.toInt(),
        countryNameEn: json['countryNameEn'] as String?,
        countryNameAr: json['countryNameAr'] as String?,
        photoRelativePath: json['photoRelativePath'] as String?,
      );
}

/// One of a speaker's scheduled sessions — mirrors
/// `SIMF.Contracts.Programme.PublicSpeakerSession`. Times are zone-free on the
/// wire.
@immutable
class SpeakerSession {
  const SpeakerSession({
    required this.id,
    required this.code,
    required this.title,
    required this.titleArabic,
    required this.hallName,
    required this.hallNameArabic,
    required this.start,
    required this.end,
  });

  final String id;
  final String code;
  final String title;
  final String titleArabic;
  final String hallName;
  final String hallNameArabic;
  final DateTime start;
  final DateTime end;

  DateTime get startLocal => saudiOf(start);

  String localizedTitle(bool isArabic) => _pick(titleArabic, title, isArabic);
  String? localizedHall(bool isArabic) =>
      _pickOpt(hallNameArabic, hallName, isArabic);

  static SpeakerSession fromJson(Map<String, dynamic> json) => SpeakerSession(
        id: json['id'] as String? ?? '',
        code: json['code'] as String? ?? '',
        title: json['title'] as String? ?? '',
        titleArabic: json['titleArabic'] as String? ?? '',
        hallName: json['hallName'] as String? ?? '',
        hallNameArabic: json['hallNameArabic'] as String? ?? '',
        start: _utc(json['start']),
        end: _utc(json['end']),
      );
}

/// The full public speaker profile — mirrors
/// `SIMF.Contracts.Programme.PublicSpeakerDetail` (`GET /app/speakers/{id}`).
/// Carries the four CV pairs, the consent gates ([allowsMeetingRequests] /
/// [allowsDataSharing]), the opted-in social URLs and the speaker's sessions.
@immutable
class SpeakerDetail {
  const SpeakerDetail({
    required this.id,
    required this.name,
    required this.nameArabic,
    required this.allowsMeetingRequests,
    required this.allowsDataSharing,
    required this.displayOrder,
    required this.sessions,
    this.rank,
    this.rankArabic,
    this.countryId,
    this.countryNameEn,
    this.countryNameAr,
    this.bio,
    this.bioArabic,
    this.qualifications,
    this.qualificationsArabic,
    this.trainingExperience,
    this.trainingExperienceArabic,
    this.awards,
    this.awardsArabic,
    this.facebookUrl,
    this.linkedInUrl,
    this.xUrl,
    this.websiteUrl,
    this.photoRelativePath,
  });

  final String id;
  final String name;
  final String nameArabic;
  final String? rank;
  final String? rankArabic;
  final int? countryId;
  final String? countryNameEn;
  final String? countryNameAr;
  final String? bio;
  final String? bioArabic;
  final String? qualifications;
  final String? qualificationsArabic;
  final String? trainingExperience;
  final String? trainingExperienceArabic;
  final String? awards;
  final String? awardsArabic;
  final bool allowsMeetingRequests;
  final bool allowsDataSharing;
  final String? facebookUrl;
  final String? linkedInUrl;
  final String? xUrl;

  /// Personal/professional website (D-544) — a 4th opted-in link, gated by
  /// [allowsDataSharing] like the social URLs. Wire key `websiteUrl`.
  final String? websiteUrl;
  final String? photoRelativePath;
  final int displayOrder;
  final List<SpeakerSession> sessions;

  String localizedName(bool isArabic) => _pick(nameArabic, name, isArabic);
  String? localizedRank(bool isArabic) =>
      _pickOpt(rankArabic, rank, isArabic);
  String? localizedCountry(bool isArabic) =>
      _pickOpt(countryNameAr, countryNameEn, isArabic);

  /// The nationality flag emoji for the profile header (Figma 908-2110),
  /// resolved from the ISO 3166-1 numeric [countryId] via the shared
  /// [countryFlagEmoji] helper. Null when no country is set / unknown — the
  /// same
  /// helper the speaker list card uses.
  String? get flagEmoji => countryFlagEmoji(countryId);

  String? localizedBio(bool isArabic) => _pickOpt(bioArabic, bio, isArabic);
  String? localizedQualifications(bool isArabic) =>
      _pickOpt(qualificationsArabic, qualifications, isArabic);
  String? localizedTraining(bool isArabic) =>
      _pickOpt(trainingExperienceArabic, trainingExperience, isArabic);
  String? localizedAwards(bool isArabic) =>
      _pickOpt(awardsArabic, awards, isArabic);

  static SpeakerDetail fromJson(Map<String, dynamic> json) => SpeakerDetail(
        id: json['id'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        rank: json['rank'] as String?,
        rankArabic: json['rankArabic'] as String?,
        countryId: (json['countryId'] as num?)?.toInt(),
        countryNameEn: json['countryNameEn'] as String?,
        countryNameAr: json['countryNameAr'] as String?,
        bio: json['bio'] as String?,
        bioArabic: json['bioArabic'] as String?,
        qualifications: json['qualifications'] as String?,
        qualificationsArabic: json['qualificationsArabic'] as String?,
        trainingExperience: json['trainingExperience'] as String?,
        trainingExperienceArabic: json['trainingExperienceArabic'] as String?,
        awards: json['awards'] as String?,
        awardsArabic: json['awardsArabic'] as String?,
        allowsMeetingRequests: json['allowsMeetingRequests'] as bool? ?? false,
        allowsDataSharing: json['allowsDataSharing'] as bool? ?? false,
        facebookUrl: json['facebookUrl'] as String?,
        linkedInUrl: json['linkedInUrl'] as String?,
        xUrl: json['xUrl'] as String?,
        websiteUrl: json['websiteUrl'] as String?,
        photoRelativePath: json['photoRelativePath'] as String?,
        displayOrder: (json['displayOrder'] as num?)?.toInt() ?? 0,
        sessions: (json['sessions'] as List? ?? const <dynamic>[])
            .whereType<Map<dynamic, dynamic>>()
            .map((e) => SpeakerSession.fromJson(e.cast<String, dynamic>()))
            .toList(growable: false),
      );
}

DateTime _utc(Object? value) {
  if (value is String && value.isNotEmpty) {
    final parsed = parseWireOrNull(value);
    if (parsed != null) {
      return parsed;
    }
  }
  return DateTime.fromMillisecondsSinceEpoch(0, isUtc: true);
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
