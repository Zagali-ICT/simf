import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The horizontal pill-tab bar shared by the session-summaries (Figma 1388:8392),
/// my-sessions (1388:9067) and presentations (1388:7621) screens: a scrollable
/// row of rounded pills, the active one filled gold, the rest navy with a beige
/// hairline. Order follows the list given — under RTL the first label sits on the
/// right (matching the Figma).
class SessionFilterTabs extends StatelessWidget {
  const SessionFilterTabs({
    required this.labels,
    required this.selectedIndex,
    required this.onSelected,
    super.key,
  });

  final List<String> labels;
  final int selectedIndex;
  final ValueChanged<int> onSelected;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
      child: Row(
        children: <Widget>[
          for (var i = 0; i < labels.length; i++)
            Padding(
              padding: EdgeInsets.only(
                left: i == 0 ? 0 : SimfTokens.space2,
              ),
              child: _Pill(
                label: labels[i],
                selected: i == selectedIndex,
                onTap: () => onSelected(i),
              ),
            ),
        ],
      ),
    );
  }
}

class _Pill extends StatelessWidget {
  const _Pill({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: selected ? SimfTokens.accent : SimfTokens.navyDeep,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        side: const BorderSide(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space4,
            vertical: SimfTokens.space2,
          ),
          child: Text(
            label,
            style: TextStyle(
              color: selected ? SimfTokens.navy : Colors.white,
              fontSize: SimfTokens.textMd,
              fontWeight: selected ? FontWeight.w600 : FontWeight.w400,
            ),
          ),
        ),
      ),
    );
  }
}
