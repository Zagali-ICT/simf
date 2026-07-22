import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// One CV section (bio / qualifications / training / awards) — a title and the
/// optional localized body (null when the speaker has no content for it).
class SpeakerCvSection {
  const SpeakerCvSection(this.title, this.body);

  final String title;
  final String? body;
}

/// The CV tab strip (frame 908:2110 `912:2312`): a full-width row of equal
/// 48-high pills — the active one filled gold with white text, the rest navy
/// `#192B41` with a beige hairline and beige text. One pill per CV section that
/// carries content.
class SpeakerCvTabs extends StatelessWidget {
  const SpeakerCvTabs({
    required this.titles,
    required this.activeIndex,
    required this.onSelect,
    super.key,
  });

  final List<String> titles;
  final int activeIndex;
  final ValueChanged<int> onSelect;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        for (var i = 0; i < titles.length; i++) ...<Widget>[
          // Frame 912:2312 — four equal pills with an ~16px gap (SimfTokens.space4)
          if (i > 0) const SizedBox(width: SimfTokens.space4),
          Expanded(
            child: _CvTab(
              label: titles[i],
              selected: i == activeIndex,
              onTap: () => onSelect(i),
            ),
          ),
        ],
      ],
    );
  }
}

class _CvTab extends StatelessWidget {
  const _CvTab({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: Container(
        height: SimfTokens.controlHeight,
        alignment: Alignment.center,
        padding: const EdgeInsets.all(SimfTokens.space2),
        decoration: BoxDecoration(
          // Figma 912:2312 — the inactive pill is border-only (no fill); it
          // reads the navySurface scaffold through, the active pill is gold.
          color: selected ? SimfTokens.accent : SimfTokens.transparent,
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          border: selected
              ? null
              : Border.all(
                  color: SimfTokens.beigeBorder,
                  width: SimfTokens.hairline,
                ),
        ),
        child: Text(
          label,
          textAlign: TextAlign.center,
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            color: selected ? SimfTokens.surface : SimfTokens.beigeBorder,
            fontWeight: FontWeight.w600,
            fontSize: SimfTokens.textSm,
            height: 1.2,
          ),
        ),
      ),
    );
  }
}

/// The CV body card (frame 908:2110 `912:2331`): the navy `#192B41` fill on the
/// 8px radius, right-aligned white body text at the frame's 21px line-height.
class SpeakerCvCard extends StatelessWidget {
  const SpeakerCvCard({required this.body, super.key});

  final String body;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      // Figma 912:2331 — px-8 / py-16 inside the navy card.
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space4,
      ),
      decoration: const BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
      ),
      child: Text(
        body,
        textAlign: TextAlign.start,
        style: SimfTokens.bodyWhite,
      ),
    );
  }
}
