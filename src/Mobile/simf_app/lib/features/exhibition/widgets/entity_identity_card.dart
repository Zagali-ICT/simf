import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/exhibition/widgets/entity_link_row.dart';
import 'package:simf_app/features/exhibition/widgets/location_line.dart';
import 'package:simf_app/features/exhibition/widgets/tier_pill.dart';

/// The borderless navyDeep identity card (Figma 1439:11891): the square logo,
/// the entity name, the gold "City، Country" line, the centred tier pill and
/// the optional stand-code→map row.
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
            Center(
                child: SizedBox(
                    width: SimfTokens.entityIdentityCardWidth,
                    height: SimfTokens.entityIdentityCardHeight,
                    child: logo,),),
            const SizedBox(height: SimfTokens.space4),
            Text(
              name,
              textAlign: TextAlign.center,
              style: SimfTokens.labelWhiteBoldXxl, // 22
            ),
            if ((locationLine ?? '').trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              LocationLine(text: locationLine!.trim(), countryId: countryId),
            ],
            if ((tierPill ?? '').trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              // The Column stretches its children; the pill must HUG its label
              // and centre (Figma 1439:11898 — a ~151px content-width pill
              // centred in the card, not a full-width bar). Center escapes the
              // stretch; the Row below sizes to content (MainAxisSize.min).
              Center(child: TierPill(label: tierPill!.trim())),
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
