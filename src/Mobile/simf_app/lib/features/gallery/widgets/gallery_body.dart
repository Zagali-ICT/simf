import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/gallery/data/media_models.dart';
import 'package:simf_app/features/gallery/widgets/media_grid.dart';

/// The gallery tab content: the الصور (images) section over the الفيديوهات
/// (videos) section, each a two-up grid of media tiles. A section renders only
/// when it has items (the frame shows both, but the wire may carry just one
/// kind). The whole body scrolls as one.
class GalleryBody extends StatelessWidget {
  const GalleryBody({
    required this.items,
    required this.isArabic,
    required this.baseUrl,
    required this.imagesLabel,
    required this.videosLabel,
    super.key,
  });

  final List<MediaItem> items;
  final bool isArabic;
  final String baseUrl;
  final String imagesLabel;
  final String videosLabel;

  @override
  Widget build(BuildContext context) {
    final images =
        items.where((m) => m.kind == MediaKind.image).toList(growable: false);
    final videos =
        items.where((m) => m.kind == MediaKind.video).toList(growable: false);
    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        0,
        SimfTokens.space4,
        SimfTokens.space6,
      ),
      children: <Widget>[
        if (images.isNotEmpty) ...<Widget>[
          // Frame nodes 949:4042/4047 — white 16px Medium, inline-start aligned.
          SimfSectionHeader(title: imagesLabel),
          const SizedBox(height: SimfTokens.space4),
          MediaGrid(items: images, isArabic: isArabic, baseUrl: baseUrl),
        ],
        if (videos.isNotEmpty) ...<Widget>[
          if (images.isNotEmpty) const SizedBox(height: SimfTokens.space6),
          SimfSectionHeader(title: videosLabel),
          const SizedBox(height: SimfTokens.space4),
          MediaGrid(items: videos, isArabic: isArabic, baseUrl: baseUrl),
        ],
      ],
    );
  }
}
