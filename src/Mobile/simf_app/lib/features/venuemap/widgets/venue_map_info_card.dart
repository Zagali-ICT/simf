import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';
import 'package:simf_app/features/venuemap/widgets/venue_map_logo_badge.dart';

/// The bottom white info card for the selected node (frame node 215:562's
/// SAMI card): the exhibitor **logo badge** · gold code box · name +
/// exhibitor/sector line, then the single gold أرشدني action (Figma 758:1358
/// shows no details button).
///
/// FR-LGO-005 — the frame's 60×60 logo badge is rendered. It was dropped when
/// booths had no logo assets to draw; they do now (BoothLogo, D-357/D-764), so
/// the badge shows the selected booth's own mark, falling back to the booth
/// short name. The dismiss control keeps its own place beside it.
class VenueMapInfoCard extends StatelessWidget {
  const VenueMapInfoCard({
    required this.l10n,
    required this.node,
    required this.booth,
    required this.baseUrl,
    required this.onDirect,
    required this.onClose,
    this.onDetails,
    super.key,
  });

  final AppL10n l10n;
  final VenueMapNode node;
  final BoothSummary? booth;

  /// API base the anonymous asset route hangs off (`{base}/app/assets/…`).
  final String baseUrl;
  final VoidCallback onDirect;
  final VoidCallback onClose;
  final VoidCallback? onDetails;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    final title = booth?.localizedName(isArabic: isArabic) ?? node.localizedLabel(isArabic: isArabic);
    final subtitleParts = <String>[
      if (booth?.localizedExhibitor(isArabic: isArabic) != null)
        booth!.localizedExhibitor(isArabic: isArabic)!,
      if (booth?.localizedSector(isArabic: isArabic) != null)
        booth!.localizedSector(isArabic: isArabic)!,
    ];
    final code = booth?.code;

    // Frame 758:1358 — white card, 8-px radius, gold 0.5 hairline + soft shadow.
    return Container(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space2,
        SimfTokens.space4,
        SimfTokens.space4,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.surface,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: SimfTokens.accent,
          width: SimfTokens.hairlineBold,
        ),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: SimfTokens.cardShadow,
            offset: Offset(0, 1),
            blurRadius: SimfTokens.venueMapInfoCardBlurRadius,
          ),
        ],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Row(
            children: <Widget>[
              // FR-LGO-005 — the exhibitor logo badge (frame 215:562).
              if (booth != null) ...<Widget>[
                VenueMapLogoBadge(
                  boothId: booth!.id,
                  baseUrl: baseUrl,
                  name: booth!.localizedName(isArabic: isArabic),
                ),
                const SizedBox(width: SimfTokens.space3),
              ],
              if (code != null)
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: SimfTokens.space3,
                    vertical: SimfTokens.space2,
                  ),
                  decoration: BoxDecoration(
                    // Frame — pale-beige with a gold hairline.
                    color: SimfTokens.codeBoxBeige,
                    borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                    border: Border.all(
                      color: SimfTokens.accent,
                      width: SimfTokens.hairlineBold,
                    ),
                  ),
                  child: Text(
                    code,
                    textDirection: TextDirection.ltr,
                    style: SimfTokens.labelGoldSemibold,
                  ),
                ),
              if (code != null) const SizedBox(width: SimfTokens.space3),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      title,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      // Frame — navy #01132D, 14px SemiBold.
                      style: SimfTokens.labelNavySemibold,
                    ),
                    if (subtitleParts.isNotEmpty) ...<Widget>[
                      const SizedBox(height: SimfTokens.space2),
                      Text(
                        subtitleParts.join(' · '),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: SimfTokens.bodyGreySm,
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              // Dismiss control (kept alongside the badge above).
              IconButton(
                onPressed: onClose,
                tooltip: MaterialLocalizations.of(context).closeButtonTooltip,
                icon: const Icon(
                  Icons.close,
                  size: SimfTokens.venueMapInfoCardSizeMd,
                  color: SimfTokens.greyText,
                ),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space3),
          Row(
            children: <Widget>[
              Expanded(
                child: FilledButton.icon(
                  onPressed: onDirect,
                  style: FilledButton.styleFrom(
                    minimumSize: const Size.fromHeight(SimfTokens.tapTarget),
                  ),
                  icon: const Icon(Icons.navigation_outlined, size: SimfTokens.venueMapInfoCardSizeSm),
                  label: Text(l10n.venueMapDirectMe),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
