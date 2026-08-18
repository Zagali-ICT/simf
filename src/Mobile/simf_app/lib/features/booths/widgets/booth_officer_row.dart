import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/utils/initials.dart';

/// The booth-officer row (frame 922:2800): the officer's gold name over the
/// fixed beige role label, beside a gold initials tile (e.g. "RS"). D-432 — the
/// officer contact now ships on the wire (server resolves it Contact-first).
class BoothOfficerRow extends StatelessWidget {
  const BoothOfficerRow({required this.name, required this.l10n, super.key});

  final String name;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                name,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: SimfTokens.labelGoldSemiboldSm,
              ),
              const SizedBox(height: SimfTokens.space1),
              Text(l10n.boothsOfficerRole, style: SimfTokens.bodyBeigeXs),
            ],
          ),
        ),
        const SizedBox(width: SimfTokens.space2),
        Container(
          width: SimfTokens.space10,
          height: SimfTokens.space10,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: SimfTokens.accent,
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
          child: Text(
            initialsFromStart(name),
            textDirection: TextDirection.ltr,
            style: SimfTokens.labelNavyBoldSm,
          ),
        ),
      ],
    );
  }
}
