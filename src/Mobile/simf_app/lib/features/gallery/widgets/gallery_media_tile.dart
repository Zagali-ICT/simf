import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../data/media_models.dart';
import 'gallery_placeholder_box.dart';

/// One media tile (frame node 949:4043): a rounded bitmap with a navy
/// bottom-gradient; video tiles overlay a centred play glyph. The localised
/// title is laid over the gradient at the inline start (the frame's chip area).
class GalleryMediaTile extends StatelessWidget {
  const GalleryMediaTile({
    required this.item,
    required this.isArabic,
    required this.baseUrl,
    super.key,
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
                colors: <Color>[SimfTokens.transparent, SimfTokens.bannerScrim],
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
                  style: SimfTokens.labelWhiteSemiboldXs,
                ),
              ),
            ),
        ],
      ),
    );
  }
}

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
          color: SimfTokens.navyFill70,
          shape: BoxShape.circle,
        ),
        alignment: Alignment.center,
        child: const Icon(
          Icons.play_arrow_rounded,
          size: 30,
          color: SimfTokens.surface,
        ),
      ),
    );
  }
}

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
      return GalleryPlaceholderBox(isVideo: isVideo);
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
          GalleryPlaceholderBox(isVideo: isVideo),
    );
  }
}
