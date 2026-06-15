import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';

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

/// `GET /app/media` → the media items (public, D-199).
final mediaItemsProvider =
    FutureProvider.autoDispose<List<MediaItem>>((ref) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<List<MediaItem>>(
    '/app/media',
    decodeData: (data) =>
        ((data is Map ? data['items'] : null) as List? ?? const <dynamic>[])
            .whereType<Map<dynamic, dynamic>>()
            .map((e) => MediaItem.fromJson(e.cast<String, dynamic>()))
            .toList(growable: false),
  );
});

/// Page 030 — معرض الصور والفيديوهات · Media gallery (#30, `/media`, Guest+).
///
/// **Public.** A grid of media tiles (image / video kind + title/album). Tiles
/// with an uploaded bitmap render it from the public `…/app/media/{id}/…`
/// route (thumbnail preferred, image fallback) with a loading spinner and a
/// graceful fall-back to the kind icon when there is no bitmap or the fetch
/// fails. Video *playback* (opening the external `VideoUrl`) is still deferred.
class GalleryScreen extends ConsumerWidget {
  const GalleryScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final media = ref.watch(mediaItemsProvider);
    // The data-package base URL already includes `/api/v1`; the tile builds
    // `{base}/app/media/{id}/(thumbnail|image)` from it.
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    return Scaffold(
      appBar: AppBar(leading: const SimfBackButton(), title: Text(l10n.galleryTitle)),
      body: SafeArea(
        child: media.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (_, __) => _Error(
            message: l10n.galleryError,
            onRetry: () => ref.invalidate(mediaItemsProvider),
          ),
          data: (items) {
            if (items.isEmpty) {
              return _Empty(message: l10n.galleryEmpty);
            }
            final isArabic = l10n.isArabic;
            return GridView.builder(
              padding: const EdgeInsets.all(SimfTokens.space4),
              gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                crossAxisCount: 2,
                mainAxisSpacing: SimfTokens.space3,
                crossAxisSpacing: SimfTokens.space3,
                childAspectRatio: 1.1,
              ),
              itemCount: items.length,
              itemBuilder: (context, index) => _MediaTile(
                item: items[index],
                isArabic: isArabic,
                baseUrl: baseUrl,
              ),
            );
          },
        ),
      ),
    );
  }
}

class _MediaTile extends StatelessWidget {
  const _MediaTile({
    required this.item,
    required this.isArabic,
    required this.baseUrl,
  });

  final MediaItem item;
  final bool isArabic;
  final String baseUrl;

  @override
  Widget build(BuildContext context) {
    final title = item.localizedTitle(isArabic);
    final album = item.localizedAlbum(isArabic);
    // Prefer the lighter thumbnail for the grid; fall back to the full image;
    // null when the item carries no bitmap (then the kind icon is shown).
    final String? tileUrl = item.hasThumbnail
        ? '$baseUrl/app/media/${item.id}/thumbnail'
        : item.hasImage
            ? '$baseUrl/app/media/${item.id}/image'
            : null;
    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Expanded(
            child: _Thumbnail(
              imageUrl: tileUrl,
              isVideo: item.kind == MediaKind.video,
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(SimfTokens.space2),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                if (title != null)
                  Text(
                    title,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: SimfTokens.surface,
                      fontWeight: FontWeight.w600,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                if (album != null)
                  Text(
                    album,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: SimfTokens.txtTertiary,
                      fontSize: SimfTokens.textXs,
                    ),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// The tile bitmap: a network image (thumbnail/image) with a spinner while it
/// loads and a fall-back to the kind icon when [imageUrl] is null or the fetch
/// fails. A video tile overlays a play glyph on its poster.
class _Thumbnail extends StatelessWidget {
  const _Thumbnail({required this.imageUrl, required this.isVideo});

  final String? imageUrl;
  final bool isVideo;

  @override
  Widget build(BuildContext context) {
    final url = imageUrl;
    if (url == null) {
      return _PlaceholderBox(isVideo: isVideo);
    }
    return Image.network(
      url,
      fit: BoxFit.cover,
      width: double.infinity,
      height: double.infinity,
      gaplessPlayback: true,
      loadingBuilder: (context, child, progress) {
        if (progress == null) {
          return isVideo ? _withPlayGlyph(child) : child;
        }
        return Container(
          color: SimfTokens.navyDeep,
          alignment: Alignment.center,
          child: const SizedBox(
            width: 22,
            height: 22,
            child: CircularProgressIndicator(strokeWidth: 2),
          ),
        );
      },
      errorBuilder: (context, error, stackTrace) =>
          _PlaceholderBox(isVideo: isVideo),
    );
  }

  Widget _withPlayGlyph(Widget child) => Stack(
        fit: StackFit.expand,
        children: <Widget>[
          child,
          const Center(
            child: Icon(
              Icons.play_circle_outline,
              size: 40,
              color: SimfTokens.surface,
            ),
          ),
        ],
      );
}

/// The no-bitmap / failed-fetch fall-back: a navy box with the kind icon
/// (mirrors the original interim tile).
class _PlaceholderBox extends StatelessWidget {
  const _PlaceholderBox({required this.isVideo});

  final bool isVideo;

  @override
  Widget build(BuildContext context) => Container(
        color: SimfTokens.navyDeep,
        alignment: Alignment.center,
        child: Icon(
          isVideo ? Icons.play_circle_outline : Icons.image_outlined,
          size: 40,
          color: SimfTokens.accent,
        ),
      );
}

class _Empty extends StatelessWidget {
  const _Empty({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(
            Icons.photo_library_outlined,
            size: 56,
            color: SimfTokens.txtTertiary,
          ),
          const SizedBox(height: SimfTokens.space3),
          Text(message, style: const TextStyle(color: SimfTokens.txtTertiary)),
        ],
      ),
    );
  }
}

class _Error extends StatelessWidget {
  const _Error({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
          ],
        ),
      ),
    );
  }
}
