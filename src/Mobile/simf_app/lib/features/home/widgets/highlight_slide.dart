import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// One carousel slide (758:1239): the news image filling a rounded card with a
/// bottom scrim and the title overlaid — image + text only. Tapping opens the
/// article. The image rides the D-357 anonymous `NewsImage` route; a spinner
/// shows while it loads and a navy image-glyph box is the no-image fall-back.
class HighlightSlide extends StatelessWidget {
  const HighlightSlide({
    required this.title,
    required this.imageUrl,
    required this.onTap,
  });

  final String title;
  final String imageUrl;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Padding(
      // A small inset so the neighbouring slides peek at the edges.
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space1),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          child: Stack(
            fit: StackFit.expand,
            children: <Widget>[
              ColoredBox(
                color: SimfTokens.navy,
                child: Image.network(
                  imageUrl,
                  fit: BoxFit.cover,
                  gaplessPlayback: true,
                  loadingBuilder: (context, child, progress) =>
                      progress == null
                          ? child
                          : const Center(
                              child: SizedBox(
                                width: SimfTokens.highlightSlideWidth,
                                height: SimfTokens.highlightSlideHeight2,
                                child:
                                    CircularProgressIndicator(strokeWidth: SimfTokens.highlightSlideStrokeWidth),
                              ),
                            ),
                  errorBuilder: (context, error, stackTrace) => const Center(
                    child: Icon(
                      Icons.image_outlined,
                      size: SimfTokens.highlightSlideSize,
                      color: SimfTokens.beigeBorder,
                    ),
                  ),
                ),
              ),
              // Bottom scrim so the white title reads over any image.
              const DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.center,
                    end: Alignment.bottomCenter,
                    colors: <Color>[SimfTokens.transparent, SimfTokens.navyFill80],
                  ),
                ),
              ),
              Positioned(
                left: SimfTokens.space4,
                right: SimfTokens.space4,
                bottom: SimfTokens.space3,
                child: Text(
                  title,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: SimfTokens.labelWhiteBoldLgTall,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
