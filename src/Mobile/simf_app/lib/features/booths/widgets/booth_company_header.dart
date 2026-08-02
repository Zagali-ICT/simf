import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_logo_image.dart';
import '../../../core/country_flag.dart';
import '../../venuemap/data/venue_map_models.dart';

/// The card header (frame node 922:2556): the company **logo tile** on the
/// inline start (physical right) — the real CompanyLogo, short-name fallback —
/// with the company short name (gold) over its full name (beige — only when it
/// differs from the short name, PAR-B4) in the middle
/// and the exhibitor's **country flag** at the inline end (physical left), under
/// a gold hairline rule. Matches the frame's logo-right / flag-left RTL layout
/// (the flag is the Figma "Group" image; the app renders the country emoji).
class BoothCompanyHeader extends StatelessWidget {
  const BoothCompanyHeader({
    required this.booth,
    required this.isArabic,
    required this.baseUrl,
    super.key,
  });

  final BoothSummary booth;
  final bool isArabic;
  final String baseUrl;

  @override
  Widget build(BuildContext context) {
    final name = booth.localizedName(isArabic);
    // PAR-B4 — the exhibitor line is only worth its own row when it actually
    // says something the short name above it does not. The shipped seed carries
    // the SAME string in both fields (SIMF_App_SeedGaps.sql), which rendered the
    // company name twice on every seeded booth card.
    final exhibitor = booth.localizedExhibitor(isArabic);
    final fullName =
        exhibitor != null && exhibitor.trim() != name.trim() ? exhibitor : null;
    final flag = countryFlagEmoji(booth.countryId);
    return Container(
      padding: const EdgeInsets.only(bottom: SimfTokens.space2),
      decoration: const BoxDecoration(
        border: Border(
          bottom: BorderSide(
            color: SimfTokens.accent,
            width: SimfTokens.hairline,
          ),
        ),
      ),
      // Frame 922:2556 — RTL order: the company logo tile at the inline-start
      // (right), the name column in the middle, the country flag at the
      // inline-end (left).
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: <Widget>[
          // Frame 922:2560 — the 48×48 logo tile (the booth's own BoothLogo,
          // short-name fallback) at the inline-start (right).
          _LogoTile(
            boothId: booth.id,
            baseUrl: baseUrl,
            fallback: name,
          ),
          const SizedBox(width: SimfTokens.space2),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(name, style: SimfTokens.labelGoldMedium),
                if (fullName != null) ...<Widget>[
                  const SizedBox(height: SimfTokens.space2),
                  Text(fullName, style: SimfTokens.bodyBeigeSm),
                ],
              ],
            ),
          ),
          // Frame 1062:12911 — the country flag tile at the inline-end (left),
          // shown only when the booth carries a resolved country.
          if (flag != null) ...<Widget>[
            const SizedBox(width: SimfTokens.space2),
            _CountryFlagTile(flag: flag),
          ],
        ],
      ),
    );
  }
}

/// The booth country flag tile (frame node 1062:12911): a 40×40 rounded tile at
/// the inline-end (left) of the header. Figma renders a full flag image; the app
/// renders the country **emoji** (its only flag form), centred on the navy tile.
class _CountryFlagTile extends StatelessWidget {
  const _CountryFlagTile({required this.flag});

  final String flag;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 40,
      height: 40,
      alignment: Alignment.center,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        flag,
        textDirection: TextDirection.ltr,
        style: const TextStyle(fontSize: 28, height: 1),
      ),
    );
  }
}

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
class _LogoTile extends StatelessWidget {
  const _LogoTile({
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
              url: '$baseUrl/app/assets/BoothLogo/$id/image',
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
