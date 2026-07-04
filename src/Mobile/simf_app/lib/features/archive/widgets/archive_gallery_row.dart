import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/utils/http_url.dart';
import '../data/archive_models.dart';

/// The archive gallery row (node 24-01 "الصور والفيديو"): a horizontal strip of
/// thumbnail tiles. P6 — D-440: an image item whose `url` is an absolute http(s)
/// link renders the real photo (Image.network, cover-filled); a video item or a
/// blank/relative url falls back to the photo / play glyph placeholder.
class ArchiveGalleryRow extends StatelessWidget {
  const ArchiveGalleryRow({required this.items, required this.isArabic, super.key});

  final List<ArchiveMediaItem> items;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 104,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: items.length,
        // Frame 926:3299 — ~16px gap between the 104px gallery tiles.
        separatorBuilder: (_, __) => const SizedBox(width: SimfTokens.space4),
        itemBuilder: (context, index) =>
            _GalleryTile(item: items[index], isArabic: isArabic),
      ),
    );
  }
}

/// One gallery thumbnail (frame node 926:3299): a 104×104 rounded square — the
/// real photo (cover-filled) under a bottom-to-navy gradient scrim, or a navy
/// glyph placeholder for a video / non-servable url. No border, no caption
/// (matches the frame).
class _GalleryTile extends StatelessWidget {
  const _GalleryTile({required this.item, required this.isArabic});

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
            height: 40,
            child: DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.bottomCenter,
                  end: Alignment.topCenter,
                  colors: <Color>[
                    SimfTokens.navy.withValues(alpha: 0.8),
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
