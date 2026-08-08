import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_logo_image.dart';
import 'package:simf_app/core/net/asset_urls.dart';

/// The square booth-logo tile (frame node 922:2560): a 48×48 navy square with a
/// beige hairline. Renders the booth's own BoothLogo (the D-357 asset owned by
/// [boothId]) shown **whole** inside the tile, falling back to the booth **short
/// name** (centred, as the frame shows "SAMI") while it loads or when the booth
/// has no logo.
///
/// Owner 2026-07-26 — the mark now FITS its tile (the old `BoxFit.cover` cropped
/// wide company logos), via the shared [SimfLogoImage]. Full-size-on-tap is OFF
/// here on purpose: the tile sits inside the tappable booth card, whose tap owns
/// the navigation to the exhibitor detail — where the 108px identity logo IS
/// tappable to full size.
///
/// DEF-LGO-002 — the inset used to be horizontal-only, which left a 40x48 (NOT
/// square) content box inside the 48x48 tile while the image still asked for
/// 48x48: the clip then shaved 4px off each side of even a perfectly square
/// logo. The inset is square now and the mark is painted at the box's real
/// [_markSize], so nothing is cropped.
class BoothLogoTile extends StatelessWidget {
  const BoothLogoTile({
    required this.boothId,
    required this.baseUrl,
    required this.fallback,
  });

  final String boothId;
  final String baseUrl;
  final String fallback;

  /// Frame 922:2560 — the logo tile is 48×48 ('Size/Square' token).
  static const double _tileSize = 48;

  /// The square box the mark is painted into: the tile minus its inset on all
  /// four sides, so a square logo stays square and whole.
  static const double _markSize = _tileSize - (SimfTokens.space1 * 2);

  @override
  Widget build(BuildContext context) {
    final fallbackText = Text(
      fallback,
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      textAlign: TextAlign.center,
      style: SimfTokens.labelWhiteSemibold,
    );
    final id = boothId.trim();
    return Container(
      width: _tileSize,
      height: _tileSize,
      alignment: Alignment.center,
      padding: const EdgeInsets.all(SimfTokens.space1),
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: id.isEmpty
          ? fallbackText
          : SimfLogoImage(
              url: AssetUrls.image(baseUrl, AssetKind.boothLogo, id),
              placeholder: fallbackText,
              semanticLabel: fallback,
              width: _markSize,
              height: _markSize,
              // Decode-cap to the painted size (§4) at up to 2x DPR, so a
              // full-res logo never decodes into this per-list-item thumbnail.
              // The full-size viewer paints the uncapped image.
              cacheWidth: (_markSize * 2).round(),
              cacheHeight: (_markSize * 2).round(),
              enableFullScreen: false,
            ),
    );
  }
}
