import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// The no-bitmap / failed-fetch fall-back for a media tile: a navy box with the
/// media kind icon.
class GalleryPlaceholderBox extends StatelessWidget {
  const GalleryPlaceholderBox({required this.isVideo, super.key});

  final bool isVideo;

  @override
  Widget build(BuildContext context) => Container(
        color: SimfTokens.navyDeep,
        alignment: Alignment.center,
        child: Icon(
          isVideo ? Icons.movie_outlined : Icons.image_outlined,
          size: SimfTokens.galleryPlaceholderBoxSize,
          color: SimfTokens.accent,
        ),
      );
}
