import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';

/// A selectable radio pill (Figma 522:2151): a white pill with an 18px
/// gold-ringed radio that fills when [selected], then the [label]. Shared form
/// primitive. [textDirection] pins the radio/label order (null = follow the
/// ambient direction); the gender field forces RTL so the gold ring sits on the
/// leading edge — rightmost in Arabic — in every locale (D-698; was text-first,
/// D-674).
class SimfRadioPill extends StatelessWidget {
  const SimfRadioPill({
    required this.label,
    required this.selected,
    required this.onTap,
    this.textDirection,
    super.key,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  /// Forces the row's direction; null follows the ambient [Directionality].
  final TextDirection? textDirection;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: SimfTokens.borderRadiusSmall,
      child: Container(
        height: 48,
        decoration: const BoxDecoration(
          color: SimfTokens.scrimWhite90, // white at 90% over the beige card
          borderRadius: SimfTokens.borderRadiusSmall,
        ),
        child: Row(
          textDirection: textDirection,
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            // D-698 — the gold ring leads (rightmost under the forced RTL), the
            // label follows, per the owner's icon-first request.
            Container(
              width: 18,
              height: 18,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                border: Border.all(color: SimfTokens.accent, width: 1.2),
              ),
              alignment: Alignment.center,
              child: selected
                  ? Container(
                      width: 10,
                      height: 10,
                      decoration: const BoxDecoration(
                        shape: BoxShape.circle,
                        color: SimfTokens.accent,
                      ),
                    )
                  : null,
            ),
            const SizedBox(width: 12),
            Text(
              label,
              style: const TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w500,
                color: SimfTokens.navy,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
