import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/speakers/widgets/cv_tab.dart';

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
            child: CvTab(
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
