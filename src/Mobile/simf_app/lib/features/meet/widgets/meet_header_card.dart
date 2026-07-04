import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';

/// The smart-suggestions header card (frame `1082:15269`): a navy-deep card,
/// radius 8, with the title, the subtitle and the three topic chips.
class MeetHeaderCard extends StatelessWidget {
  const MeetHeaderCard({required this.l10n, super.key});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space2,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            l10n.meetPeopleSmartTitle,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textMd,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            l10n.meetPeopleSmartSubtitle,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
              height: 1.4,
            ),
          ),
          const SizedBox(height: SimfTokens.space4),
          Row(
            children: <Widget>[
              for (final (index, label) in <String>[
                l10n.meetPeopleFilterAi,
                l10n.meetPeopleFilterSupply,
                l10n.meetPeopleFilterSeabed,
              ].indexed) ...<Widget>[
                if (index > 0) const SizedBox(width: SimfTokens.space2),
                Expanded(child: _TopicChip(label: label)),
              ],
            ],
          ),
        ],
      ),
    );
  }
}

/// One topic chip in the header (frame `1082:15267`): beige hairline, radius 4,
/// beige 12px label.
class _TopicChip extends StatelessWidget {
  const _TopicChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      alignment: Alignment.center,
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        label,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        style: const TextStyle(
          color: SimfTokens.beigeBorder,
          fontSize: SimfTokens.textSm,
        ),
      ),
    );
  }
}
