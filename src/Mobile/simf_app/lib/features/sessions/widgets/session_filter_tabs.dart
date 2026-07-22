import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// The pill-tab bar shared by the session-summaries (Figma 1388:8392),
/// my-sessions (1388:9067) and presentations (1388:7621) screens: pills with the
/// active one filled gold with white text, the rest a transparent box with a
/// beige hairline + beige text. Order follows the list given — under RTL the
/// first label sits on the right (matching the Figma, where الجميع/all is on the
/// right).
///
/// [equalWidth] fills the row with N equal-width pills (the summaries frame's
/// fixed 3-tab layout). It defaults to **false** — a horizontally scrollable row
/// of hug-width pills — because the other consumers pass a variable number of
/// (longer) tabs: my-sessions has 4, presentations has one per event day, which
/// equal-width would clip with no way to scroll to the off-screen tabs.
class SessionFilterTabs extends StatelessWidget {
  const SessionFilterTabs({
    required this.labels,
    required this.selectedIndex,
    required this.onSelected,
    this.equalWidth = false,
    this.icons,
    this.gap = SimfTokens.space4,
    super.key,
  });

  final List<String> labels;
  final int selectedIndex;
  final ValueChanged<int> onSelected;
  final bool equalWidth;

  /// Optional per-tab leading glyphs (my-sessions 1388:9077 puts a 14px icon in
  /// each pill). One per label, or null for a text-only bar (summaries).
  final List<IconData>? icons;

  /// The gap between equal-width pills (summaries 16, my-sessions 8).
  final double gap;

  @override
  Widget build(BuildContext context) {
    if (equalWidth) {
      return Padding(
        padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
        child: Row(
          children: <Widget>[
            for (var i = 0; i < labels.length; i++) ...<Widget>[
              if (i > 0) SizedBox(width: gap),
              Expanded(
                child: _Pill(
                  label: labels[i],
                  selected: i == selectedIndex,
                  icon: icons != null && i < icons!.length ? icons![i] : null,
                  onTap: () => onSelected(i),
                ),
              ),
            ],
          ],
        ),
      );
    }
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
      child: Row(
        children: <Widget>[
          // Gap the pills with a between-sibling SizedBox, not per-pill leading
          // padding: a physical `left` pad put the gap on the wrong side in RTL
          // (no space after الكل). A SizedBox is even and direction-agnostic.
          for (var i = 0; i < labels.length; i++) ...<Widget>[
            if (i > 0) const SizedBox(width: SimfTokens.space2),
            _Pill(
              label: labels[i],
              selected: i == selectedIndex,
              onTap: () => onSelected(i),
            ),
          ],
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
    this.icon,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final Color fg = selected ? SimfTokens.surface : SimfTokens.beigeBorder;
    final Widget text = Text(
      label,
      textAlign: TextAlign.center,
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      style: TextStyle(
        color: fg,
        fontSize: SimfTokens.textSm, // 12
        fontWeight: FontWeight.w600,
      ),
    );
    return Material(
      color: selected ? SimfTokens.accent : SimfTokens.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        side: selected
            ? BorderSide.none
            : const BorderSide(
                color: SimfTokens.beigeBorder,
                width: SimfTokens.hairline,
              ),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2), // p-8
          child: icon == null
              ? text
              : Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: <Widget>[
                    Flexible(child: text),
                    const SizedBox(width: SimfTokens.space1), // gap-4
                    Icon(icon, size: 14, color: fg),
                  ],
                ),
        ),
      ),
    );
  }
}
