import 'package:flutter/material.dart';

import '../../../app/theme/app_assets.dart';
import '../../../app/theme/tokens.dart';

/// One hero background image: the uploaded banner asset, falling back to the
/// banner's pasted [fallbackUrl], then the bundled discover photo — so the hero
/// always shows something even before an image is uploaded.
class HeroImage extends StatelessWidget {
  const HeroImage({required this.url, this.fallbackUrl});

  final String url;
  final String? fallbackUrl;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: SimfTokens.navy,
      child: Image.network(
        url,
        fit: BoxFit.fill,
        gaplessPlayback: true,
        errorBuilder: (context, error, stackTrace) {
          final fallback = fallbackUrl;
          if (fallback != null && fallback.isNotEmpty) {
            return Image.network(
              fallback,
              fit: BoxFit.fill,
              errorBuilder: (_, __, ___) => _placeholder,
            );
          }
          return _placeholder;
        },
      ),
    );
  }

  Widget get _placeholder =>
      Image.asset(AppAssets.discoverHero, fit: BoxFit.fill);
}
