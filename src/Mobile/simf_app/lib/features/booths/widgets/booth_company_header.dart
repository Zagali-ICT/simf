import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_logo_image.dart';
import '../../../core/country_flag.dart';
import '../../../core/net/asset_urls.dart';
import '../../venuemap/data/venue_map_models.dart';
import 'booth_logo_tile.dart';
import 'country_flag_tile.dart';

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
          BoothLogoTile(
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
            CountryFlagTile(flag: flag),
          ],
        ],
      ),
    );
  }
}
