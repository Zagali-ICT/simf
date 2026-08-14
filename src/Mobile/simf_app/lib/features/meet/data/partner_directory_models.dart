import 'package:flutter/foundation.dart';

import 'package:simf_app/core/net/asset_urls.dart';

/// One row of the "Meet People Like You" partner directory — mirrors
/// `SIMF.Contracts.Networking.PartnerDirectoryEntry`. [kind] discriminates the
/// tap target and the logo asset: `speaker`, `sponsor`, `booth` or `person`.
/// [id] is the route id for that kind (speaker / sponsor / booth id, or the
/// opted-in account's user-profile id). Bilingual name + subtitle.
@immutable
class PartnerDirectoryEntry {
  const PartnerDirectoryEntry({
    required this.kind,
    required this.id,
    required this.name,
    required this.nameArabic,
    this.subtitle,
    this.subtitleArabic,
    this.logoRelativePath,
    this.logoContactId,
    this.countryId,
  });

  factory PartnerDirectoryEntry.fromJson(Map<String, dynamic> json) =>
      PartnerDirectoryEntry(
        kind: json['kind'] as String? ?? '',
        id: json['id'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        subtitle: json['subtitle'] as String?,
        subtitleArabic: json['subtitleArabic'] as String?,
        logoRelativePath: json['logoRelativePath'] as String?,
        logoContactId: json['logoContactId'] as String?,
        countryId: (json['countryId'] as num?)?.toInt(),
      );

  final String kind;
  final String id;
  final String name;
  final String nameArabic;

  /// Rank (speaker) / tagline (sponsor) / sector (booth) / job title (person).
  final String? subtitle;
  final String? subtitleArabic;

  /// Present when the entity has an uploaded logo/photo (drives whether the cell
  /// attempts an image vs the initials avatar).
  final String? logoRelativePath;

  /// The exhibitor's Contact id — the CompanyLogo owner for a booth company.
  final String? logoContactId;
  final int? countryId;

  bool get isSpeaker => kind == PartnerDirectoryKind.speaker;
  bool get isSponsor => kind == PartnerDirectoryKind.sponsor;
  bool get isBooth => kind == PartnerDirectoryKind.booth;

  String localizedName({required bool isArabic}) {
    final ar = nameArabic.trim();
    final en = name.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  String? localizedSubtitle({required bool isArabic}) {
    final ar = subtitleArabic?.trim() ?? '';
    final en = subtitle?.trim() ?? '';
    final picked = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
    return picked.isEmpty ? null : picked;
  }

  /// The logo/photo URL for this entry, built per kind against [baseUrl] (the
  /// same asset routes the speakers / sponsors / booths screens use), or null
  /// when there is no logo — the cell then shows the initials avatar. Person
  /// rows have no logo.
  String? logoUrl(String baseUrl) {
    switch (kind) {
      case PartnerDirectoryKind.speaker:
        return logoRelativePath != null
            ? AssetUrls.image(baseUrl, AssetKind.speakerPhoto, id)
            : null;
      case PartnerDirectoryKind.sponsor:
        return logoRelativePath != null
            ? AssetUrls.image(baseUrl, AssetKind.sponsorLogo, id)
            : null;
      case PartnerDirectoryKind.booth:
        return logoContactId != null
            ? AssetUrls.image(baseUrl, AssetKind.companyLogo, logoContactId!)
            : null;
      default:
        return null;
    }
  }

  /// Reads `PartnerDirectoryResponse = { entries: [...] }` into the rows.
  static List<PartnerDirectoryEntry> listFromData(Object? data) =>
      ((data is Map ? data['entries'] : null) as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => PartnerDirectoryEntry.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false);
}

/// The [PartnerDirectoryEntry.kind] wire tokens (mirrors
/// `SIMF.Contracts.Networking.PartnerDirectoryKind`).
class PartnerDirectoryKind {
  PartnerDirectoryKind._();

  static const String speaker = 'speaker';
  static const String sponsor = 'sponsor';
  static const String booth = 'booth';
  static const String person = 'person';
}
