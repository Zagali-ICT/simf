import 'package:flutter/foundation.dart';

/// Media kind — mirrors `MediaKind` (int wire: Image=0, Video=1).
enum MediaKind {
  image,
  video;

  static MediaKind fromJson(Object? value) {
    if (value is num && value.toInt() == 1) {
      return MediaKind.video;
    }
    if (value is String && value == 'Video') {
      return MediaKind.video;
    }
    return MediaKind.image;
  }
}

/// One media item — mirrors `PublicMediaItem` (`GET /app/media`).
///
/// The wire carries `imageUrl` / `thumbnailUrl` (server-relative, non-null only
/// when bytes were uploaded). We keep them as the [hasImage] / [hasThumbnail]
/// presence flags rather than the raw strings: the actual bitmap is fetched
/// from the public route `…/app/media/{id}/(thumbnail|image)` built against the
/// data-package base URL (the wire string omits the `/app` segment, so it is a
/// presence signal, not a fetch URL).
@immutable
class MediaItem {
  const MediaItem({
    required this.id,
    required this.kind,
    this.title,
    this.titleArabic,
    this.album,
    this.albumArabic,
    this.hasImage = false,
    this.hasThumbnail = false,
  });

  final String id;
  final MediaKind kind;
  final String? title;
  final String? titleArabic;
  final String? album;
  final String? albumArabic;
  final bool hasImage;
  final bool hasThumbnail;

  String? localizedTitle(bool isArabic) => _pick(titleArabic, title, isArabic);
  String? localizedAlbum(bool isArabic) => _pick(albumArabic, album, isArabic);

  static MediaItem fromJson(Map<String, dynamic> json) => MediaItem(
        id: json['id'] as String? ?? '',
        kind: MediaKind.fromJson(json['kind']),
        title: json['title'] as String?,
        titleArabic: json['titleArabic'] as String?,
        album: json['album'] as String?,
        albumArabic: json['albumArabic'] as String?,
        hasImage: (json['imageUrl'] as String?)?.isNotEmpty ?? false,
        hasThumbnail: (json['thumbnailUrl'] as String?)?.isNotEmpty ?? false,
      );
}

String? _pick(String? arabic, String? english, bool isArabic) {
  final ar = arabic?.trim() ?? '';
  final en = english?.trim() ?? '';
  final value = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  return value.isEmpty ? null : value;
}
