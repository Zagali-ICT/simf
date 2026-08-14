import 'package:flutter/foundation.dart';

/// One sponsor — mirrors `SIMF.Contracts.*.PublicSponsor`. Note the wire names
/// are `nameEn` / `nameAr` (not `name`/`nameArabic`). The social/contact cluster
/// is optional (D-287).
@immutable
class Sponsor {
  const Sponsor({
    required this.id,
    required this.nameEn,
    required this.nameAr,
    required this.tierName,
    required this.displayOrder,
    this.logoRelativePath,
    this.url,
    this.email,
    this.phonePrimary,
    this.tagline,
    this.taglineArabic,
    this.countryId,
  });

  factory Sponsor.fromJson(Map<String, dynamic> json) => Sponsor(
        id: json['id'] as String? ?? '',
        nameEn: json['nameEn'] as String? ?? '',
        nameAr: json['nameAr'] as String? ?? '',
        tierName: json['tierName'] as String? ?? '',
        displayOrder: (json['displayOrder'] as num?)?.toInt() ?? 0,
        logoRelativePath: json['logoRelativePath'] as String?,
        url: json['url'] as String?,
        email: json['email'] as String?,
        phonePrimary: json['phonePrimary'] as String?,
        tagline: json['tagline'] as String?,
        taglineArabic: json['taglineArabic'] as String?,
        countryId: (json['countryId'] as num?)?.toInt(),
      );

  final String id;
  final String nameEn;
  final String nameAr;
  final String tierName;
  final int displayOrder;
  final String? logoRelativePath;
  final String? url;
  final String? email;
  final String? phonePrimary;
  // D-432 — optional bilingual tagline shown under the name (Figma 922:2824).
  final String? tagline;
  final String? taglineArabic;
  // D-456 — the sponsor's country (from the linked Contact), ISO 3166-1 numeric,
  // for the corner flag on the logo. Null when unset.
  final int? countryId;

  String localizedName(bool isArabic) {
    final ar = nameAr.trim();
    final en = nameEn.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  /// The locale-appropriate tagline, or null when neither language is set.
  String? localizedTagline(bool isArabic) {
    final ar = (taglineArabic ?? '').trim();
    final en = (tagline ?? '').trim();
    final primary = isArabic ? ar : en;
    if (primary.isNotEmpty) {
      return primary;
    }
    final fallback = isArabic ? en : ar;
    return fallback.isEmpty ? null : fallback;
  }
}

/// The full sponsor detail — mirrors `SIMF.Contracts.Sponsors.PublicSponsorDetail`
/// (`GET /app/sponsors/{id}`, Figma 1439:11826). Adds the "نبذة عن الراعي" about
/// paragraph + the city to the card cluster; tier / website / country are the same
/// fields the list carries.
@immutable
class SponsorDetail {
  const SponsorDetail({
    required this.id,
    required this.nameEn,
    required this.nameAr,
    required this.tier,
    required this.tierName,
    this.logoRelativePath,
    this.url,
    this.about,
    this.aboutArabic,
    this.city,
    this.cityArabic,
    this.countryId,
    this.countryNameEn,
    this.countryNameAr,
  });

  factory SponsorDetail.fromData(Object? data) {
    final json = (data as Map?)?.cast<String, dynamic>() ??
        const <String, dynamic>{};
    return SponsorDetail(
      id: json['id'] as String? ?? '',
      nameEn: json['nameEn'] as String? ?? '',
      nameAr: json['nameAr'] as String? ?? '',
      tier: (json['tier'] as num?)?.toInt() ?? 0,
      tierName: json['tierName'] as String? ?? '',
      logoRelativePath: json['logoRelativePath'] as String?,
      url: json['url'] as String?,
      about: json['about'] as String?,
      aboutArabic: json['aboutArabic'] as String?,
      city: json['city'] as String?,
      cityArabic: json['cityArabic'] as String?,
      countryId: (json['countryId'] as num?)?.toInt(),
      countryNameEn: json['countryNameEn'] as String?,
      countryNameAr: json['countryNameAr'] as String?,
    );
  }

  final String id;
  final String nameEn;
  final String nameAr;
  final int tier;
  final String tierName;
  final String? logoRelativePath;
  final String? url;
  final String? about;
  final String? aboutArabic;
  final String? city;
  final String? cityArabic;
  final int? countryId;
  final String? countryNameEn;
  final String? countryNameAr;

  String localizedName(bool isArabic) =>
      _pickRequired(nameAr, nameEn, isArabic);

  String? localizedAbout(bool isArabic) =>
      _pickOptional(aboutArabic, about, isArabic);

  String? localizedCity(bool isArabic) =>
      _pickOptional(cityArabic, city, isArabic);

  String? localizedCountry(bool isArabic) =>
      _pickOptional(countryNameAr, countryNameEn, isArabic);
}

String _pickRequired(String arabic, String english, bool isArabic) {
  final ar = arabic.trim();
  final en = english.trim();
  return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
}

String? _pickOptional(String? arabic, String? english, bool isArabic) {
  final ar = arabic?.trim() ?? '';
  final en = english?.trim() ?? '';
  final value = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  return value.isEmpty ? null : value;
}

/// A tier band of sponsors — mirrors `PublicSponsorTierGroup`.
@immutable
class SponsorTierGroup {
  const SponsorTierGroup({
    required this.tier,
    required this.tierName,
    required this.sponsors,
  });

  factory SponsorTierGroup.fromJson(Map<String, dynamic> json) =>
      SponsorTierGroup(
        tier: (json['tier'] as num?)?.toInt() ?? 0,
        tierName: json['tierName'] as String? ?? '',
        sponsors: (json['sponsors'] as List? ?? const <dynamic>[])
            .whereType<Map<dynamic, dynamic>>()
            .map((e) => Sponsor.fromJson(e.cast<String, dynamic>()))
            .toList(growable: false),
      );

  final int tier;
  final String tierName;
  final List<Sponsor> sponsors;

  /// Reads `PublicSponsors = { groups: [...] }` into the tier bands.
  static List<SponsorTierGroup> listFromData(Object? data) =>
      ((data is Map ? data['groups'] : null) as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => SponsorTierGroup.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);
}
