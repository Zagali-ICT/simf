import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../data/archive_models.dart';
import 'archive_gallery_tile.dart';

/// The archive gallery row (node 24-01 "الصور والفيديو"): a horizontal strip of
/// thumbnail tiles. P6 — D-440: an image item whose `url` is an absolute http(s)
/// link renders the real photo (Image.network, cover-filled); a video item or a
/// blank/relative url falls back to the photo / play glyph placeholder.
class ArchiveGalleryRow extends StatelessWidget {
  const ArchiveGalleryRow({
    required this.items,
    required this.isArabic,
    super.key,
  });

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
            ArchiveGalleryTile(item: items[index], isArabic: isArabic),
      ),
    );
  }
}
