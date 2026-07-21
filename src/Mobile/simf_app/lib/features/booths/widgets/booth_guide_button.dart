import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/app_assets.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_svg_icon.dart';

/// The full-width gold guide-me CTA (frame node 922:2820): "أرشدني إلى الجناح ·
/// A-12" with a trailing location glyph, white text on the gold fill. #9 — opens
/// the venue map focused on this booth (the inner InkWell captures the tap so it
/// does not also bubble to the card's open-detail onTap).
class BoothGuideButton extends StatelessWidget {
  const BoothGuideButton({
    required this.code,
    required this.l10n,
    required this.onTap,
    super.key,
  });

  final String code;
  final AppL10n l10n;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.accent,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space4,
            vertical: SimfTokens.space3,
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Flexible(
                child: Text(
                  l10n.boothsGuideMe(code),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: SimfTokens.labelWhiteSemiboldSm,
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              const SimfSvgIcon(
                AppAssets.navLocation,
                size: 18,
                color: SimfTokens.surface,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
