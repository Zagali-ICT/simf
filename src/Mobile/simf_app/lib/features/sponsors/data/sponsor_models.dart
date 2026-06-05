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
  });

  final String id;
  final String nameEn;
  final String nameAr;
  final String tierName;
  final int displayOrder;
  final String? logoRelativePath;
  final String? url;
  final String? email;
  final String? phonePrimary;

  String localizedName(bool isArabic) {
    final ar = nameAr.trim();
    final en = nameEn.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  static Sponsor fromJson(Map<String, dynamic> json) => Sponsor(
        id: json['id'] as String? ?? '',
        nameEn: json['nameEn'] as String? ?? '',
        nameAr: json['nameAr'] as String? ?? '',
        tierName: json['tierName'] as String? ?? '',
        displayOrder: (json['displayOrder'] as num?)?.toInt() ?? 0,
        logoRelativePath: json['logoRelativePath'] as String?,
        url: json['url'] as String?,
        email: json['email'] as String?,
        phonePrimary: json['phonePrimary'] as String?,
      );
}

/// A tier band of sponsors — mirrors `PublicSponsorTierGroup`.
@immutable
class SponsorTierGroup {
  const SponsorTierGroup({
    required this.tier,
    required this.tierName,
    required this.sponsors,
  });

  final int tier;
  final String tierName;
  final List<Sponsor> sponsors;

  static SponsorTierGroup fromJson(Map<String, dynamic> json) =>
      SponsorTierGroup(
        tier: (json['tier'] as num?)?.toInt() ?? 0,
        tierName: json['tierName'] as String? ?? '',
        sponsors: (json['sponsors'] as List? ?? const <dynamic>[])
            .whereType<Map<dynamic, dynamic>>()
            .map((e) => Sponsor.fromJson(e.cast<String, dynamic>()))
            .toList(growable: false),
      );

  /// Reads `PublicSponsors = { groups: [...] }` into the tier bands.
  static List<SponsorTierGroup> listFromData(Object? data) =>
      ((data is Map ? data['groups'] : null) as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => SponsorTierGroup.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);
}
