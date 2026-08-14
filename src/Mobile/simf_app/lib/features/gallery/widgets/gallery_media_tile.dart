import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/gallery/data/media_models.dart';
import 'package:simf_app/features/gallery/widgets/play_glyph.dart';
import 'package:simf_app/features/gallery/widgets/thumbnail.dart';

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
    final title = item.localizedTitle(isArabic: isArabic);
    final isVideo = item.kind == MediaKind.video;
    final tileUrl = mediaTileUrl(item, baseUrl);
    return ClipRRect(
      borderRadius: const BorderRadius.all(Radius.circular(SimfTokens.radius)),
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
