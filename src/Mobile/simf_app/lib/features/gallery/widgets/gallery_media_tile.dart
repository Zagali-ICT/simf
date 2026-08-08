import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../data/media_models.dart';
import 'play_glyph.dart';
import 'thumbnail.dart';

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
          Thumbnail(imageUrl: tileUrl, isVideo: isVideo),
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
          if (isVideo) const PlayGlyph(),
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

