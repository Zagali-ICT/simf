import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/responsive/grid_columns.dart';
import 'package:simf_app/features/gallery/data/media_models.dart';
import 'package:simf_app/features/gallery/widgets/gallery_media_tile.dart';

/// A two-up grid of media tiles (frame: 104-high, 8-radius tiles with a
/// 12-gap).
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
      gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: responsiveGridColumns(context, compact: 2),
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
