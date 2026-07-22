import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../core/country_flag.dart';
import 'entity_link_row.dart';

/// The borderless navyDeep identity card (Figma 1439:11891): the square logo,
/// the entity name, the gold "City، Country" line, the centred tier pill and the
/// optional stand-code→map row.
class EntityIdentityCard extends StatelessWidget {
  const EntityIdentityCard({
    required this.logo,
    required this.name,
    required this.locationLine,
    required this.countryId,
    required this.tierPill,
    required this.standLabel,
    required this.standCode,
    required this.onMap,
    super.key,
  });

  final Widget logo;
  final String name;
  final String? locationLine;
  final int? countryId;
  final String? tierPill;
  final String? standLabel;
  final String? standCode;
  final VoidCallback? onMap;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
      radius: SimfTokens.radius, // 8
      borderWidth: 0, // borderless (Figma 1439:11891)
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Center(child: SizedBox(width: 108, height: 108, child: logo)),
            const SizedBox(height: SimfTokens.space4),
            Text(
              name,
              textAlign: TextAlign.center,
              style: SimfTokens.labelWhiteBoldXxl, // 22
            ),
            if ((locationLine ?? '').trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              _LocationLine(text: locationLine!.trim(), countryId: countryId),
            ],
            if ((tierPill ?? '').trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              // The Column stretches its children; the pill must HUG its label
              // and centre (Figma 1439:11898 — a ~151px content-width pill
              // centred in the card, not a full-width bar). Center escapes the
              // stretch; the Row below sizes to content (MainAxisSize.min).
              Center(child: _TierPill(label: tierPill!.trim())),
            ],
            if ((standCode ?? '').trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              EntityLinkRow(
                label: standLabel ?? '',
                value: standCode!.trim(),
                icon: Icons.place_outlined,
                onTap: onMap,
                // Stand→map row (Figma 1439:11904): navy fill, value above
                // label, value Bold-16, label Medium-12.
                background: SimfTokens.navy,
                valueOnTop: true,
                valueSize: SimfTokens.textLg,
                valueWeight: FontWeight.w700,
                labelWeight: FontWeight.w500,
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// The gold "City، Country" line with the country flag (Figma 1439:11895):
/// SemiBold-14 gold city, 20px flag, 8px gap, flag on the left (RTL).
class _LocationLine extends StatelessWidget {
  const _LocationLine({required this.text, required this.countryId});

  final String text;
  final int? countryId;

  @override
  Widget build(BuildContext context) {
    final flag = countryFlagEmoji(countryId);
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        Flexible(
          child: Text(
            text,
            textAlign: TextAlign.center,
            style: SimfTokens.labelGoldSemibold, // 14
          ),
        ),
        if (flag != null) ...<Widget>[
          const SizedBox(width: SimfTokens.space2),
          Text(
            flag,
            textDirection: TextDirection.ltr,
            style: const TextStyle(fontSize: SimfTokens.textXl, height: 1), // 20
          ),
        ],
      ],
    );
  }
}

/// The full-width tier pill (Figma 1439:11898): beige-10% fill, beige hairline,
/// radius-8, px-20/py-8, gap-8; the 16px medal glyph at the inline start (right
/// in RTL, node 1439:11899) then the gold Bold-14 label (node 1439:11903).
class _TierPill extends StatelessWidget {
  const _TierPill({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space5, // 20
        vertical: SimfTokens.space2, // 8
      ),
      decoration: BoxDecoration(
        color: SimfTokens.beigeFill10,
        borderRadius: BorderRadius.circular(SimfTokens.radius), // 8
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          const Icon(
            Icons.workspace_premium_outlined,
            size: 16,
            color: SimfTokens.accent,
          ),
          const SizedBox(width: SimfTokens.space2),
          Flexible(
            child: Text(
              label,
              textAlign: TextAlign.center,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: SimfTokens.labelGoldBold, // 14
            ),
          ),
        ],
      ),
    );
  }
}
