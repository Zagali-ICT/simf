import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/gallery/widgets/gallery_placeholder_box.dart';

/// The tile bitmap: a network image (thumbnail/image) with a spinner while it
/// loads and a fall-back to the kind icon when [imageUrl] is null or the fetch
/// fails.
class Thumbnail extends StatelessWidget {
  const Thumbnail({required this.imageUrl, required this.isVideo, super.key});

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
            width: SimfTokens.thumbnailWidth,
            height: SimfTokens.thumbnailHeight,
            child: CircularProgressIndicator(
                strokeWidth: SimfTokens.thumbnailStrokeWidth,),
          ),
        );
      },
      errorBuilder: (context, error, stackTrace) =>
          GalleryPlaceholderBox(isVideo: isVideo),
    );
  }
}
