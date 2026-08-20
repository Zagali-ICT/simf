import 'package:flutter/foundation.dart';
import 'package:simf_app/core/country_flag.dart';
import 'package:simf_app/core/utils/bilingual.dart';
import 'package:simf_app/core/utils/saudi_time.dart';

/// One row in the public speakers list — mirrors
/// `SIMF.Contracts.Programme.PublicSpeakerSummary` (`GET /app/speakers`). The
/// card shows the avatar, the bilingual name, the rank line and the country
/// (flag from [countryId] — interim renders the name). The avatar URL is built
/// from the speaker id via `AssetUrls.image`, never from a path on the wire:
/// the server's `photoRelativePath` is permanently null now and was decoded
/// here, unread, until it was removed.
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
  });

  factory SpeakerSummary.fromJson(Map<String, dynamic> json) => SpeakerSummary(
        id: json['id'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        displayOrder: (json['displayOrder'] as num?)?.toInt() ?? 0,
        rank: json['rank'] as String?,
        rankArabic: json['rankArabic'] as String?,
        countryId: (json['countryId'] as num?)?.toInt(),
        countryNameEn: json['countryNameEn'] as String?,
        countryNameAr: json['countryNameAr'] as String?,
      );

  final String id;
  final String name;
  final String nameArabic;
  final int displayOrder;
  final String? rank;
  final String? rankArabic;
  final int? countryId;
  final String? countryNameEn;
  final String? countryNameAr;

  String localizedName({required bool isArabic}) =>
      pickLocalized(nameArabic, name, isArabic: isArabic);
  String? localizedRank({required bool isArabic}) =>
      pickLocalizedOrNull(rankArabic, rank, isArabic: isArabic);
  String? localizedCountry({required bool isArabic}) =>
      pickLocalizedOrNull(countryNameAr, countryNameEn, isArabic: isArabic);

  /// Matches a free-text search over the localized name and BOTH rank
  /// spellings, so a search finds a speaker whichever language the rank was
  /// entered in. An empty query matches everything.
  ///
  /// Mirrors `DelegationItem.matches`. The speakers screen and the meeting
  /// request sheet each wrote this predicate out; the sheet adds its own rule
  /// on top (the already-selected speaker always survives the filter).
  bool matches(String query, {required bool isArabic}) {
    final q = query.trim().toLowerCase();
    if (q.isEmpty) {
      return true;
    }
    return localizedName(isArabic: isArabic).toLowerCase().contains(q) ||
        (rank ?? '').toLowerCase().contains(q) ||
        (rankArabic ?? '').toLowerCase().contains(q);
  }
}

/// The speakers a list should show for [query], alphabetically by localized
/// name when [alphaSorted]. Lifted out of `SpeakersScreen`'s build path.
List<SpeakerSummary> visibleSpeakers(
  List<SpeakerSummary> speakers,
  String query, {
  required bool isArabic,
  bool alphaSorted = false,
}) {
  final list = speakers
      .where((speaker) => speaker.matches(query, isArabic: isArabic))
      .toList();
  if (alphaSorted) {
    list.sort(
      (a, b) => a
          .localizedName(isArabic: isArabic)
          .compareTo(b.localizedName(isArabic: isArabic)),
    );
  }
  return list;
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

  factory SpeakerSession.fromJson(Map<String, dynamic> json) => SpeakerSession(
        id: json['id'] as String? ?? '',
        code: json['code'] as String? ?? '',
        title: json['title'] as String? ?? '',
        titleArabic: json['titleArabic'] as String? ?? '',
        hallName: json['hallName'] as String? ?? '',
        hallNameArabic: json['hallNameArabic'] as String? ?? '',
        start: _utc(json['start']),
        end: _utc(json['end']),
      );

  final String id;
  final String code;
  final String title;
  final String titleArabic;
  final String hallName;
  final String hallNameArabic;
  final DateTime start;
  final DateTime end;

  DateTime get startLocal => saudiOf(start);

  String localizedTitle({required bool isArabic}) =>
      pickLocalized(titleArabic, title, isArabic: isArabic);
  String? localizedHall({required bool isArabic}) =>
      pickLocalizedOrNull(hallNameArabic, hallName, isArabic: isArabic);
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
  });

  factory SpeakerDetail.fromJson(Map<String, dynamic> json) => SpeakerDetail(
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
        displayOrder: (json['displayOrder'] as num?)?.toInt() ?? 0,
        sessions: (json['sessions'] as List? ?? const <dynamic>[])
            .whereType<Map<dynamic, dynamic>>()
            .map((e) => SpeakerSession.fromJson(e.cast<String, dynamic>()))
            .toList(growable: false),
      );

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
  final int displayOrder;
  final List<SpeakerSession> sessions;

  String localizedName({required bool isArabic}) =>
      pickLocalized(nameArabic, name, isArabic: isArabic);
  String? localizedRank({required bool isArabic}) =>
      pickLocalizedOrNull(rankArabic, rank, isArabic: isArabic);
  String? localizedCountry({required bool isArabic}) =>
      pickLocalizedOrNull(countryNameAr, countryNameEn, isArabic: isArabic);

  /// The nationality flag emoji for the profile header (Figma 908-2110),
  /// resolved from the ISO 3166-1 numeric [countryId] via the shared
  /// [countryFlagEmoji] helper. Null when no country is set / unknown — the
  /// same
  /// helper the speaker list card uses.
  String? get flagEmoji => countryFlagEmoji(countryId);

  String? localizedBio({required bool isArabic}) =>
      pickLocalizedOrNull(bioArabic, bio, isArabic: isArabic);
  String? localizedQualifications({required bool isArabic}) =>
      pickLocalizedOrNull(
        qualificationsArabic,
        qualifications,
        isArabic: isArabic,
      );
  String? localizedTraining({required bool isArabic}) =>
      pickLocalizedOrNull(
        trainingExperienceArabic,
        trainingExperience,
        isArabic: isArabic,
      );
  String? localizedAwards({required bool isArabic}) =>
      pickLocalizedOrNull(awardsArabic, awards, isArabic: isArabic);
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

/// D-474 (#11) — one bookable meeting slot offered by a speaker.
class SpeakerSlot {
  const SpeakerSlot({required this.start, required this.end});

  factory SpeakerSlot.fromJson(Map<String, dynamic> json) => SpeakerSlot(
        start: DateTime.parse(json['start'] as String),
        end: DateTime.parse(json['end'] as String),
      );

  final DateTime start;
  final DateTime end;
}
