import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// The no-image / failed-fetch fall-back: a navy box with the article glyph.
class NewsImageFallback extends StatelessWidget {
  const NewsImageFallback();

  @override
  Widget build(BuildContext context) => const ColoredBox(
        color: SimfTokens.navy,
        child: Center(
          child: Icon(
            Icons.article_outlined,
            size: SimfTokens.newsImageFallbackSize,
            color: SimfTokens.accent,
          ),
        ),
      );
}
