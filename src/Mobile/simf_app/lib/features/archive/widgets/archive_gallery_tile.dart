import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/utils/http_url.dart';
import '../data/archive_models.dart';

/// One gallery thumbnail (frame node 926:3299): a 104×104 rounded square — the
/// real photo (cover-filled) under a bottom-to-navy gradient scrim, or a navy
/// glyph placeholder for a video / non-servable url. No border, no caption
/// (matches the frame).
class ArchiveGalleryTile extends StatelessWidget {
  const ArchiveGalleryTile({
    required this.item,
    required this.isArabic,
    super.key,
  });

  final ArchiveMediaItem item;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final showImage = !item.isVideo && isHttpUrl(item.url);
    return Container(
      width: 104,
      height: 104,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Stack(
        fit: StackFit.expand,
        children: <Widget>[
          if (showImage)
            Image.network(
              item.url,
              fit: BoxFit.cover,
              loadingBuilder: (context, child, progress) =>
                  progress == null ? child : _placeholder,
              errorBuilder: (context, error, stackTrace) => _placeholder,
            )
          else
            _placeholder,
          // Frame's bottom-to-rgba(0,16,48,0.8) scrim.
          Positioned(
            left: 0,
            right: 0,
            bottom: 0,
            height: SimfTokens.galleryScrimHeight,
            child: DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.bottomCenter,
                  end: Alignment.topCenter,
                  colors: <Color>[
                    SimfTokens.navy.withValues(
                      alpha: SimfTokens.scrimOpacityStrong,
                    ),
                    SimfTokens.navy.withValues(alpha: 0),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget get _placeholder => Center(
        child: Icon(
          item.isVideo ? Icons.play_circle_outline : Icons.image_outlined,
          color: SimfTokens.accent,
          size: 28,
        ),
      );
}
