import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';

/// The square logo on an exhibitor / sponsor detail card (Figma 1439:11881 /
/// 11826): the real CompanyLogo / SponsorLogo asset (served anonymously per
/// D-357) clipped to fill, falling back to the entity initials while it loads or
/// when no logo is set (the asset route 404s) or [url] is null.
class EntityLogoImage extends StatelessWidget {
  const EntityLogoImage({required this.url, required this.initials, super.key});

  final String? url;
  final String initials;

  @override
  Widget build(BuildContext context) {
    final fallback = Center(
      child: Text(
        initials,
        textDirection: TextDirection.ltr,
        style: SimfTokens.labelWhiteBoldXl,
      ),
    );
    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: (url == null || url!.isEmpty)
          ? fallback
          : Image.network(
              url!,
              fit: BoxFit.cover,
              gaplessPlayback: true,
              loadingBuilder: (context, child, progress) =>
                  progress == null ? child : fallback,
              errorBuilder: (context, error, stackTrace) => fallback,
            ),
    );
  }
}
