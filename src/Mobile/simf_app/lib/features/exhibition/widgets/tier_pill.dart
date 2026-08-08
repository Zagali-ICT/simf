import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// The full-width tier pill (Figma 1439:11898): beige-10% fill, beige hairline,
/// radius-8, px-20/py-8, gap-8; the 16px medal glyph at the inline start (right
/// in RTL, node 1439:11899) then the gold Bold-14 label (node 1439:11903).
class TierPill extends StatelessWidget {
  const TierPill({required this.label});

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
            size: SimfTokens.tierPillSize,
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
