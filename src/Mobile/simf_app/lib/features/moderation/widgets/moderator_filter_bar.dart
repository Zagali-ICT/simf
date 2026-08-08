import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/moderation_models.dart';
import 'moderation_chip.dart';

/// The moderator queue filter bar (Figma 805:1876): five equal-width chips
/// filling the row (الكل / جديد / الأسئلة المقبولة / تمت الإجابة / مرفوض), each
/// with its live count.
class ModeratorFilterBar extends StatelessWidget {
  const ModeratorFilterBar({
    required this.l10n,
    required this.filter,
    required this.counts,
    required this.onChanged,
    super.key,
  });

  final AppL10n l10n;
  final ModeratorQueueFilter filter;
  final Map<ModeratorQueueFilter, int> counts;
  final ValueChanged<ModeratorQueueFilter> onChanged;

  String _label(ModeratorQueueFilter f) {
    switch (f) {
      case ModeratorQueueFilter.all:
        return l10n.moderatorChipAll;
      case ModeratorQueueFilter.fresh:
        return l10n.moderatorChipNew;
      case ModeratorQueueFilter.accepted:
        return l10n.moderatorChipAccepted;
      case ModeratorQueueFilter.answered:
        return l10n.moderatorChipAnswered;
      case ModeratorQueueFilter.rejected:
        return l10n.moderatorChipRejected;
    }
  }

  @override
  Widget build(BuildContext context) {
    final filters = ModeratorQueueFilter.values;
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space6,
        SimfTokens.space4,
        SimfTokens.space6,
        SimfTokens.space2,
      ),
      child: Row(
        children: <Widget>[
          for (int i = 0; i < filters.length; i++) ...<Widget>[
            if (i > 0) const SizedBox(width: SimfTokens.space4),
            Expanded(
              child: ModerationChip(
                label: _label(filters[i]),
                count: counts[filters[i]] ?? 0,
                active: filter == filters[i],
                onTap: () => onChanged(filters[i]),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

