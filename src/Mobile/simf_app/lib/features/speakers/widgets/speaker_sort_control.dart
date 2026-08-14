import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// The speakers-list sort control (frame 908:1744) — a navy rounded box with a
/// sort glyph, the "ترتيب حسب الابجدية" label and a direction chevron; tapping
/// flips the alphabetical order of the list.
class SpeakerSortControl extends StatelessWidget {
  const SpeakerSortControl({
    required this.label,
    required this.selected,
    required this.onTap,
    super.key,
  });

  final String label;

  /// Whether the alphabetical sort is currently applied (gold-highlighted).
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final tint = selected ? SimfTokens.accent : SimfTokens.beigeBorder;
    return Material(
      color: SimfTokens.navyDeep,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: Container(
          height: SimfTokens.controlHeight,
          // Frame 1341:3583 — 8px horizontal padding, 0.2px beige hairline.
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(SimfTokens.radius),
            border: Border.all(
              color: tint,
              width: selected ? 1 : SimfTokens.hairline,
            ),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Icon(Icons.swap_vert,
                  size: SimfTokens.speakerSortControlSize, color: tint,),
              const SizedBox(width: SimfTokens.space2),
              Text(
                label,
                maxLines: 1,
                // Frame 1341:3582 — Inter Medium 12px, beige (#C2B8A2).
                style: TextStyle(
                  color: tint,
                  fontWeight: FontWeight.w500,
                  fontSize: SimfTokens.textSm,
                ),
              ),
              const SizedBox(width: SimfTokens.space1),
              Icon(Icons.keyboard_arrow_down,
                  size: SimfTokens.speakerSortControlSize, color: tint,),
            ],
          ),
        ),
      ),
    );
  }
}
