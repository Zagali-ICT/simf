import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The deep-navy hall box (frame node 922:2624): a 48-high `#01132D` box with a
/// beige hairline. When the hall ships both names it reads "HALL A · القاعة
/// الرئيسية" — the [code] (gold) + a beige dot + the [name] (beige); otherwise it
/// falls back to a single gold [name] label.
class BoothHallBox extends StatelessWidget {
  const BoothHallBox({required this.name, this.code, super.key});

  /// The beige hall name (the frame's Arabic name), or — when [code] is null —
  /// the single gold fallback label (localized hall name / sector / generic).
  final String name;

  /// The gold hall code (the frame's upper-cased English hall name), or null for
  /// the single-tone fallback.
  final String? code;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: SimfTokens.controlHeight,
      alignment: Alignment.center,
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: code == null
          ? Text(
              name,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              textAlign: TextAlign.center,
              style: SimfTokens.labelGoldSemibold,
            )
          // Frame 922:2624 — pinned LTR so the gold "HALL A" leads and the beige
          // Arabic name trails, matching the frame regardless of the app's RTL.
          : Row(
              textDirection: TextDirection.ltr,
              mainAxisAlignment: MainAxisAlignment.center,
              children: <Widget>[
                Text(code!, style: SimfTokens.labelGoldSemibold),
                const SizedBox(width: SimfTokens.space2),
                const Text('·', style: SimfTokens.labelBeigeBold),
                const SizedBox(width: SimfTokens.space2),
                Flexible(
                  child: Text(
                    name,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: SimfTokens.labelBeigeSemibold,
                  ),
                ),
              ],
            ),
    );
  }
}
