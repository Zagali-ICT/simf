import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';

/// A tappable 1–5 star bar. Renders in the ambient direction so the fill grows
/// from the inline start (right under RTL — matching the Figma — left under
/// LTR). [value] 0 means unscored (all outlines). Tapping star N sets [value] N.
class StarRow extends StatelessWidget {
  const StarRow({
    required this.value,
    required this.onChanged,
    this.size = 24,
    this.gap = SimfTokens.space2,
    super.key,
  });

  final int value;
  final ValueChanged<int> onChanged;
  final double size;
  final double gap;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        for (var star = 1; star <= 5; star++) ...<Widget>[
          if (star > 1) SizedBox(width: gap),
          // Each star is a bare glyph, so the whole rating control was five
          // unnamed tappables and a screen-reader user could not submit a
          // rating at all (BUG-012). [selected] reports the current score.
          Semantics(
            button: true,
            selected: star <= value,
            label: l10n.rateStarLabel(star),
            child: GestureDetector(
              behavior: HitTestBehavior.opaque,
              onTap: () => onChanged(star),
              child: Padding(
                padding:
                    const EdgeInsets.symmetric(vertical: SimfTokens.space1),
                child: Icon(
                  star <= value
                      ? Icons.star_rounded
                      : Icons.star_outline_rounded,
                  size: size,
                  color: star <= value
                      ? SimfTokens.accent
                      : SimfTokens.beigeBorder,
                ),
              ),
            ),
          ),
        ],
      ],
    );
  }
}
