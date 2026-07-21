import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../data/media_models.dart';
import 'gallery_media_tile.dart';

/// A two-up grid of media tiles (frame: 104-high, 8-radius tiles with a 12-gap).
class MediaGrid extends StatelessWidget {
  const MediaGrid({
    required this.items,
    required this.isArabic,
    required this.baseUrl,
    super.key,
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
        childAspectRatio: SimfTokens.mediaTileAspectRatio,
      ),
      itemCount: items.length,
      itemBuilder: (context, index) => GalleryMediaTile(
        item: items[index],
        isArabic: isArabic,
        baseUrl: baseUrl,
      ),
    );
  }
}
