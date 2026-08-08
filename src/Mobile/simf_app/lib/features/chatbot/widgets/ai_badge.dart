import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The gold "AI" tag prefixing every assistant bubble (frame `1064:13276`):
/// a gold pill, white bold "AI" at 12px.
class AiBadge extends StatelessWidget {
  const AiBadge();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.gap2,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        AppL10n.of(context).aiBadgeLabel,
        style: SimfTokens.labelWhiteBold12Tall,
      ),
    );
  }
}
