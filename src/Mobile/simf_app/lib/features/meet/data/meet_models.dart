import 'package:flutter/foundation.dart';

/// One shared interest between the signed-in visitor and a recommended profile —
/// mirrors `SIMF.Contracts.*.MatchedInterest`. Bilingual `name`/`nameArabic`.
@immutable
class MatchedInterest {
  const MatchedInterest({
    required this.id,
    required this.name,
    required this.nameArabic,
  });

  final String id;
  final String name;
  final String nameArabic;

  String localizedName(bool isArabic) {
    final ar = nameArabic.trim();
    final en = name.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  static MatchedInterest fromJson(Map<String, dynamic> json) => MatchedInterest(
        id: json['id'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
      );
}

/// One recommended profile ("meet someone like you") — mirrors
/// `SIMF.Contracts.*.RecommendationEntry`. The English/Arabic names plus the
/// optional job title and bilingual profile-type label drive the card; the
/// shared-interest cluster + count + score come from the match scorer.
@immutable
class Recommendation {
  const Recommendation({
    required this.userProfileId,
    required this.englishName,
    required this.arabicName,
    required this.sharedInterests,
    required this.sharedInterestCount,
    required this.score,
    this.jobTitle,
    this.profileTypeName,
    this.profileTypeNameArabic,
  });

  final String userProfileId;
  final String englishName;
  final String arabicName;
  final String? jobTitle;
  final String? profileTypeName;
  final String? profileTypeNameArabic;
  final List<MatchedInterest> sharedInterests;
  final int sharedInterestCount;
  final double score;

  String localizedName(bool isArabic) {
    final ar = arabicName.trim();
    final en = englishName.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  /// The bilingual profile-type label, or `null` when neither side is set.
  String? localizedProfileType(bool isArabic) {
    final ar = profileTypeNameArabic?.trim() ?? '';
    final en = profileTypeName?.trim() ?? '';
    final picked = isArabic
        ? (ar.isNotEmpty ? ar : en)
        : (en.isNotEmpty ? en : ar);
    return picked.isEmpty ? null : picked;
  }

  static Recommendation fromJson(Map<String, dynamic> json) => Recommendation(
        userProfileId: json['userProfileId'] as String? ?? '',
        englishName: json['englishName'] as String? ?? '',
        arabicName: json['arabicName'] as String? ?? '',
        jobTitle: json['jobTitle'] as String?,
        profileTypeName: json['profileTypeName'] as String?,
        profileTypeNameArabic: json['profileTypeNameArabic'] as String?,
        sharedInterests: (json['sharedInterests'] as List? ?? const <dynamic>[])
            .whereType<Map<dynamic, dynamic>>()
            .map((e) => MatchedInterest.fromJson(e.cast<String, dynamic>()))
            .toList(growable: false),
        sharedInterestCount:
            (json['sharedInterestCount'] as num?)?.toInt() ?? 0,
        score: (json['score'] as num?)?.toDouble() ?? 0,
      );

  /// Reads `RecommendationsResponse = { matches: [...] }` into the entries.
  static List<Recommendation> listFromData(Object? data) =>
      ((data is Map ? data['matches'] : null) as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => Recommendation.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);
}
