import 'package:flutter/foundation.dart';

import '../../../core/net/asset_urls.dart';

/// One media partner — mirrors `PublicMediaPartnerItem` (`name`/`nameArabic`).
@immutable
class MediaPartner {
  const MediaPartner({
    required this.id,
    required this.name,
    required this.nameArabic,
    this.logoRelativePath,
  });

  final String id;
  final String name;
  final String nameArabic;

  /// Legacy free-text path carried on the public wire — **not** the rendered
  /// logo source. The card renders the partner's uploaded logo from the D-357
  /// asset route (see [logoAssetUrl]); this field (which historically held an
  /// arbitrary path / placeholder URL) is kept only to mirror the wire shape.
  final String? logoRelativePath;

  String localizedName(bool isArabic) {
    final ar = nameArabic.trim();
    final en = name.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  /// The public, anonymous URL that serves this partner's uploaded logo bytes
  /// via the D-357 unified media-asset pipeline (the one place the route shape
  /// lives). [baseUrl] already includes the `/api/v1` segment. The route 404s
  /// when the partner has no uploaded logo, so the caller falls back to the
  /// partner's initials.
  String logoAssetUrl(String baseUrl) =>
      AssetUrls.image(baseUrl, AssetKind.mediaPartnerLogo, id);

  static MediaPartner fromJson(Map<String, dynamic> json) => MediaPartner(
        id: json['id'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        logoRelativePath: json['logoRelativePath'] as String?,
      );
}
