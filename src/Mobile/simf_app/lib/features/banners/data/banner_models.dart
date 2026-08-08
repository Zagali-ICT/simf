import '../../../core/net/asset_urls.dart';

/// One public home banner (`GET /app/banners`, D-173) — the rotating home-hero
/// image source (#43). The image is an uploaded asset served by the row id
/// (`/app/assets/Banner/{id}/image`); [imageUrl] is an optional pasted-URL
/// fallback. Title / body are the CP-entered announcement copy (the hero overlays
/// the edition config instead of these, but they are decoded for completeness).
class PublicBannerItem {
  const PublicBannerItem({
    required this.id,
    required this.title,
    required this.titleArabic,
    required this.body,
    required this.bodyArabic,
    required this.displayOrder,
    this.imageUrl,
    this.linkUrl,
  });

  final String id;
  final String title;
  final String titleArabic;
  final String body;
  final String bodyArabic;
  final int displayOrder;
  final String? imageUrl;
  final String? linkUrl;

  String titleFor(bool isArabic) => isArabic ? titleArabic : title;

  /// The uploaded banner image served by the row id. `baseUrl` already includes
  /// `/api/v1`, so it is not re-appended. A 404 (no upload) → the caller falls
  /// back to [imageUrl] then a placeholder.
  String assetImageUrl(String baseUrl) =>
      AssetUrls.image(baseUrl, AssetKind.banner, id);

  static PublicBannerItem fromJson(Map<String, dynamic> json) =>
      PublicBannerItem(
        id: json['id'] as String? ?? '',
        title: json['title'] as String? ?? '',
        titleArabic: json['titleArabic'] as String? ?? '',
        body: json['body'] as String? ?? '',
        bodyArabic: json['bodyArabic'] as String? ?? '',
        displayOrder: (json['displayOrder'] as num?)?.toInt() ?? 0,
        imageUrl: json['imageUrl'] as String?,
        linkUrl: json['linkUrl'] as String?,
      );

  /// Decode the `GET /app/banners` payload (`{ "items": [ … ] }`).
  static List<PublicBannerItem> listFromData(dynamic data) {
    final map =
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
    final items = (map['items'] as List?) ?? const <dynamic>[];
    return items
        .map((m) =>
            PublicBannerItem.fromJson((m as Map).cast<String, dynamic>()))
        .toList();
  }
}
