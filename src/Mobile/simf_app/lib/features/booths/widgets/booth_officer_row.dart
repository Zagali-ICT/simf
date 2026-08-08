import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';

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
            _initials(name),
            textDirection: TextDirection.ltr,
            style: SimfTokens.labelNavyBoldSm,
          ),
        ),
      ],
    );
  }
}

/// The first two letters of a booth name, upper-cased, for the officer tile.
String _initials(String name) {
  final trimmed = name.trim();
  if (trimmed.isEmpty) {
    return '';
  }
  return trimmed.substring(0, trimmed.length >= 2 ? 2 : 1).toUpperCase();
}
