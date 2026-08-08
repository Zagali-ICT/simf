import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_logo_image.dart';
import 'package:simf_app/core/net/asset_urls.dart';

/// The selected booth's logo badge — its own BoothLogo (D-357) shown whole via
/// the shared [SimfLogoImage], falling back to the booth NAME on the navy tile
/// while it loads or when the booth has no logo (the same short-name fallback
/// the booths list uses; the code already has its own chip beside this badge).
/// Full-size-on-tap is off: the card is an overlay whose actions are the أرشدني
/// CTA and the dismiss control.
/// Frame 215:562 - the badge is a 60x60 rounded tile.
const double _badgeSize = 60;

class VenueMapLogoBadge extends StatelessWidget {
  const VenueMapLogoBadge({
    required this.boothId,
    required this.baseUrl,
    required this.name,
  });

  final String boothId;
  final String baseUrl;
  final String name;

  @override
  Widget build(BuildContext context) {
    final fallbackTile = Center(
      child: Text(
        name,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        textAlign: TextAlign.center,
        style: SimfTokens.labelWhiteSemibold,
      ),
    );
    final id = boothId.trim();
    return Container(
      width: _badgeSize,
      height: _badgeSize,
      alignment: Alignment.center,
      padding: const EdgeInsets.all(SimfTokens.space1),
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: id.isEmpty
          ? fallbackTile
          : SimfLogoImage(
              url: AssetUrls.image(baseUrl, AssetKind.boothLogo, id),
              placeholder: fallbackTile,
              semanticLabel: name,
              // Decode-cap to the painted badge at up to 2x DPR.
              cacheWidth: (_badgeSize * 2).round(),
              cacheHeight: (_badgeSize * 2).round(),
              enableFullScreen: false,
            ),
    );
  }
}
