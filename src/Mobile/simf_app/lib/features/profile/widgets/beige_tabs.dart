import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

const BorderRadius _radius4 =
    BorderRadius.all(Radius.circular(SimfTokens.radiusSmall));

/// The design's beige segmented tabs (Figma 505:1075 / 505:1030) — D-373
/// owner fix: the **selected** segment is a **white pill** with navy text
/// (the old selected-beige-on-beige rendered invisible); the unselected
/// segment stays on the container beige with white text.
class BeigeTabs extends StatelessWidget {
  const BeigeTabs({
    required this.options,
    required this.selectedIndex,
    required this.onChanged,
    super.key,
  });

  final List<String> options;
  final int selectedIndex;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
      decoration: const BoxDecoration(
        color: SimfTokens.beigeBorder,
        borderRadius: _radius4,
      ),
      child: Row(
        children: <Widget>[
          for (int i = 0; i < options.length; i++) ...<Widget>[
            if (i > 0) const SizedBox(width: 1),
            Expanded(
              child: InkWell(
                onTap: () => onChanged(i),
                borderRadius: _radius4,
                child: Container(
                  height: 34,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: i == selectedIndex
                        ? Colors.white
                        : Colors.transparent,
                    borderRadius: _radius4,
                  ),
                  child: Text(
                    options[i],
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: i == selectedIndex
                          ? FontWeight.w600
                          : FontWeight.w500,
                      color:
                          i == selectedIndex ? SimfTokens.navy : Colors.white,
                    ),
                  ),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}
