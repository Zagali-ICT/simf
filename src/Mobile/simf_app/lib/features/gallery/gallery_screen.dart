import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
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

/// Page 030 — التغطية الإعلامية · معرض الصور والفيديوهات · Media gallery
/// (#30, `/media`, Guest+), rebuilt to the KSA-Project frame **947:3764** on the
/// shared shell.
///
/// **Public.** The frame is the *media-coverage* hub: a three-tab selector
/// (الأخبار · الشركاء الإعلاميون · معرض الصور والفيديوهات) over the active tab's
/// content. This screen owns the **gallery** tab; the other two tabs navigate
/// to their own routes ([RouteNames.news] / [RouteNames.mediaPartners]). The
/// gallery splits the media cache into two labelled sections — **الصور**
/// (image tiles) and **الفيديوهات** (video tiles with a centred play glyph) —
/// each a two-up grid of rounded tiles with a navy bottom-gradient. Tiles with
/// an uploaded bitmap render it from the public `…/app/media/{id}/…` route
/// (thumbnail preferred, image fallback) with a loading spinner and a graceful
/// fall-back to the kind icon when there is no bitmap or the fetch fails. Video
/// *playback* (opening the external `VideoUrl`) is still deferred.
class GalleryScreen extends ConsumerWidget {
  const GalleryScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final media = ref.watch(mediaItemsProvider);
    // The data-package base URL already includes `/api/v1`; the tile builds
    // `{base}/app/media/{id}/(thumbnail|image)` from it.
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    return KsaPage(
      title: l10n.mediaCoverageTitle,
      onBack: () => ksaBackOrHome(context),
      body: Column(
        children: <Widget>[
          Padding(
            padding: const EdgeInsets.fromLTRB(
              SimfTokens.space4,
              SimfTokens.space2,
              SimfTokens.space4,
              SimfTokens.space4,
            ),
            child: _CoverageTabs(l10n: l10n),
          ),
          Expanded(
            child: media.when(
              loading: () =>
                  const Center(child: CircularProgressIndicator()),
              error: (_, __) => KsaErrorState(
                message: l10n.galleryError,
                retryLabel: l10n.retryLabel,
                onRetry: () => ref.invalidate(mediaItemsProvider),
              ),
              data: (items) {
                if (items.isEmpty) {
                  return KsaEmptyState(
                    icon: Icons.photo_library_outlined,
                    message: l10n.galleryEmpty,
                  );
                }
                return _GalleryBody(
                  items: items,
                  isArabic: l10n.isArabic,
                  baseUrl: baseUrl,
                  imagesLabel: l10n.galleryImagesSection,
                  videosLabel: l10n.galleryVideosSection,
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

/// The three media-coverage tabs (frame node 947:3869): the active tab is solid
/// gold, the others bordered navy cards. The gallery tab is active here; the
/// other two navigate to their routes (the app models each tab as its own
/// screen — frames 948:3961 / the partners frame).
class _CoverageTabs extends StatelessWidget {
  const _CoverageTabs({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    // Figma 947:3764 (Arabic/RTL): the active معرض الصور والفيديوهات tab is the
    // right-most (inline-start), then الشركاء الإعلاميون, then الأخبار on the
    // left. A Row lays children start→end, so the order is gallery → partners →
    // news.
    return Row(
      children: <Widget>[
        Expanded(
          child: _CoverageTab(
            label: l10n.galleryTitle,
            active: true,
            onTap: null,
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: _CoverageTab(
            label: l10n.mediaPartnersTitle,
            active: false,
            onTap: () => context.goNamed(RouteNames.mediaPartners),
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: _CoverageTab(
            label: l10n.newsTitle,
            active: false,
            onTap: () => context.goNamed(RouteNames.news),
          ),
        ),
      ],
    );
  }
}

/// One tab pill (frame node 947:3872): a 48-high card, solid gold when active
/// else a bordered navy card. Two-word labels wrap to two centred lines.
class _CoverageTab extends StatelessWidget {
  const _CoverageTab({
    required this.label,
    required this.active,
    required this.onTap,
  });

  final String label;
  final bool active;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      onTap: onTap,
      color: active ? SimfTokens.accent : SimfTokens.navyDeep,
      borderColor: active ? SimfTokens.accent : SimfTokens.beigeBorder,
      child: SizedBox(
        height: 48,
        child: Center(
          child: Padding(
            padding:
                const EdgeInsets.symmetric(horizontal: SimfTokens.space1),
            child: Text(
              label,
              textAlign: TextAlign.center,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                // Figma 947:3764 — the active gold pill carries dark navy text;
                // inactive pills carry beige text on navy.
                color: active ? SimfTokens.navy : SimfTokens.beigeBorder,
                fontSize: SimfTokens.textSm,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// The gallery tab content: the الصور (images) section over the الفيديوهات
/// (videos) section, each a two-up grid of media tiles. A section renders only
/// when it has items (the frame shows both, but the wire may carry just one
/// kind). The whole body scrolls as one.
class _GalleryBody extends StatelessWidget {
  const _GalleryBody({
    required this.items,
    required this.isArabic,
    required this.baseUrl,
    required this.imagesLabel,
    required this.videosLabel,
  });

  final List<MediaItem> items;
  final bool isArabic;
  final String baseUrl;
  final String imagesLabel;
  final String videosLabel;

  @override
  Widget build(BuildContext context) {
    final images = items
        .where((m) => m.kind == MediaKind.image)
        .toList(growable: false);
    final videos = items
        .where((m) => m.kind == MediaKind.video)
        .toList(growable: false);
    return ListView(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        0,
        SimfTokens.space4,
        SimfTokens.space6,
      ),
      children: <Widget>[
        if (images.isNotEmpty) ...<Widget>[
          _SectionLabel(label: imagesLabel),
          const SizedBox(height: SimfTokens.space4),
          _MediaGrid(items: images, isArabic: isArabic, baseUrl: baseUrl),
        ],
        if (videos.isNotEmpty) ...<Widget>[
          if (images.isNotEmpty) const SizedBox(height: SimfTokens.space6),
          _SectionLabel(label: videosLabel),
          const SizedBox(height: SimfTokens.space4),
          _MediaGrid(items: videos, isArabic: isArabic, baseUrl: baseUrl),
        ],
      ],
    );
  }
}

/// A section label (frame nodes 949:4042 / 949:4047): white 16px Medium, aligned
/// to the inline start (right under RTL, as the frame shows).
class _SectionLabel extends StatelessWidget {
  const _SectionLabel({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: AlignmentDirectional.centerStart,
      child: Text(
        label,
        style: const TextStyle(
          color: Colors.white,
          fontSize: SimfTokens.textLg,
          fontWeight: FontWeight.w500,
        ),
      ),
    );
  }
}

/// A two-up grid of media tiles (frame: 104-high, 8-radius tiles with a 12-gap).
class _MediaGrid extends StatelessWidget {
  const _MediaGrid({
    required this.items,
    required this.isArabic,
    required this.baseUrl,
  });

  final List<MediaItem> items;
  final bool isArabic;
  final String baseUrl;

  @override
  Widget build(BuildContext context) {
    return GridView.builder(
      shrinkWrap: true,
      primary: false,
      physics: const NeverScrollableScrollPhysics(),
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 2,
        mainAxisSpacing: SimfTokens.space4,
        crossAxisSpacing: SimfTokens.space4,
        // Frame tiles are 164×104 → ~1.58 aspect.
        childAspectRatio: 164 / 104,
      ),
      itemCount: items.length,
      itemBuilder: (context, index) => _MediaTile(
        item: items[index],
        isArabic: isArabic,
        baseUrl: baseUrl,
      ),
    );
  }
}

/// One media tile (frame node 949:4043): a rounded bitmap with a navy
/// bottom-gradient; video tiles overlay a centred play glyph. The localised
/// title is laid over the gradient at the inline start (the frame's chip area).
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
    final isVideo = item.kind == MediaKind.video;
    // Prefer the lighter thumbnail for the grid; fall back to the full image;
    // null when the item carries no bitmap (then the kind icon is shown).
    final String? tileUrl = item.hasThumbnail
        ? '$baseUrl/app/media/${item.id}/thumbnail'
        : item.hasImage
            ? '$baseUrl/app/media/${item.id}/image'
            : null;
    return ClipRRect(
      borderRadius:
          const BorderRadius.all(Radius.circular(SimfTokens.radius)),
      child: Stack(
        fit: StackFit.expand,
        children: <Widget>[
          _Thumbnail(imageUrl: tileUrl, isVideo: isVideo),
          // The navy bottom-gradient (frame: transparent → navy-80% at the
          // bottom) so any overlaid label reads on a bright photo.
          const DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.center,
                end: Alignment.bottomCenter,
                colors: <Color>[Colors.transparent, _gradientNavy],
              ),
            ),
          ),
          if (isVideo) const _PlayGlyph(),
          if (title != null)
            Positioned(
              left: SimfTokens.space2,
              right: SimfTokens.space2,
              bottom: SimfTokens.space2,
              child: Align(
                alignment: AlignmentDirectional.centerStart,
                child: Text(
                  title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: SimfTokens.textXs,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

/// Frame gradient end colour: navy `#001030` at 80% (rgba(0,16,48,0.8)).
const Color _gradientNavy = Color(0xCC001030);

/// The video play glyph (frame node 949:4059): a navy-70% circle with a centred
/// play triangle.
class _PlayGlyph extends StatelessWidget {
  const _PlayGlyph();

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Container(
        width: 52,
        height: 52,
        decoration: const BoxDecoration(
          color: _playCircleNavy,
          shape: BoxShape.circle,
        ),
        alignment: Alignment.center,
        child: const Icon(
          Icons.play_arrow_rounded,
          size: 30,
          color: Colors.white,
        ),
      ),
    );
  }
}

/// Frame play-circle fill: navy `#01132D` at 70% (rgba(1,19,45,0.7)).
const Color _playCircleNavy = Color(0xB301132D);

/// The tile bitmap: a network image (thumbnail/image) with a spinner while it
/// loads and a fall-back to the kind icon when [imageUrl] is null or the fetch
/// fails.
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
          return child;
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
}

/// The no-bitmap / failed-fetch fall-back: a navy box with the kind icon.
class _PlaceholderBox extends StatelessWidget {
  const _PlaceholderBox({required this.isVideo});

  final bool isVideo;

  @override
  Widget build(BuildContext context) => Container(
        color: SimfTokens.navyDeep,
        alignment: Alignment.center,
        child: Icon(
          isVideo ? Icons.movie_outlined : Icons.image_outlined,
          size: 32,
          color: SimfTokens.accent,
        ),
      );
}
