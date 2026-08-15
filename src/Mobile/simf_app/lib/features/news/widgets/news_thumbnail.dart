import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/news/widgets/category_chip.dart';
import 'package:simf_app/features/news/widgets/news_image_fallback.dart';

/// The news thumbnail (frame node 958:2202): the article's `NewsImage` asset
/// (fetched from the public anonymous D-357 route) under a navy
/// bottom-gradient,
/// with the gold category chip overlaid at the inline-start top corner. A
/// spinner shows while it loads; a navy article-icon box is the no-image /
/// fetch
/// -failure fall-back. 155 wide, stretched to the card height.
class NewsThumbnail extends StatelessWidget {
  const NewsThumbnail(
      {required this.imageUrl, required this.category, super.key,});

  final String imageUrl;
  final String category;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: SimfTokens.newsThumbWidth,
      height: SimfTokens.newsThumbHeight,
      child: ClipRRect(
        borderRadius:
            const BorderRadius.all(Radius.circular(SimfTokens.radius)),
        child: Stack(
          fit: StackFit.expand,
          children: <Widget>[
            Image.network(
              imageUrl,
              fit: BoxFit.cover,
              gaplessPlayback: true,
              loadingBuilder: (context, child, progress) {
                if (progress == null) {
                  return child;
                }
                return const ColoredBox(
                  color: SimfTokens.navy,
                  child: Center(
                    child: SizedBox(
                      width: SimfTokens.newsThumbnailWidth,
                      height: SimfTokens.newsThumbnailHeight,
                      child: CircularProgressIndicator(
                          strokeWidth: SimfTokens.newsThumbnailStrokeWidth,),
                    ),
                  ),
                );
              },
              errorBuilder: (context, error, stackTrace) =>
                  const NewsImageFallback(),
            ),
            // The frame's bottom gradient (transparent → navy `#001030` @ 80%).
            const DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.center,
                  end: Alignment.bottomCenter,
                  colors: <Color>[
                    SimfTokens.transparent,
                    SimfTokens.bannerScrim,
                  ],
                ),
              ),
            ),
            if (category.isNotEmpty)
              PositionedDirectional(
                top: SimfTokens.space2,
                start: SimfTokens.space2,
                child: CategoryChip(label: category),
              ),
          ],
        ),
      ),
    );
  }
}
