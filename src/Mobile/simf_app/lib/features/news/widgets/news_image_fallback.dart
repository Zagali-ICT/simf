import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

class NewsImageFallback extends StatelessWidget {
  const NewsImageFallback({super.key});

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
