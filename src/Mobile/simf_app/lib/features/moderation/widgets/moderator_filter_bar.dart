import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/moderation_models.dart';

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
      padding: const EdgeInsets.fromLTRB(24, 16, 24, 8),
      child: Row(
        children: <Widget>[
          for (int i = 0; i < filters.length; i++) ...<Widget>[
            if (i > 0) const SizedBox(width: SimfTokens.space4),
            Expanded(
              child: _Chip(
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

class _Chip extends StatelessWidget {
  const _Chip({
    required this.label,
    required this.count,
    required this.active,
    required this.onTap,
  });

  final String label;
  final int count;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: Container(
        height: 58,
        padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
        decoration: BoxDecoration(
          color: active ? SimfTokens.accent : SimfTokens.navyDeep,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          border: Border.all(
            color: active ? SimfTokens.accent : SimfTokens.navyDisabledBorder,
            width: 1.18,
          ),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Flexible(
              child: Text(
                label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: active ? Colors.white : SimfTokens.beigeBorder,
                  fontWeight: active ? FontWeight.w700 : FontWeight.w400,
                  fontSize: SimfTokens.textTitle,
                ),
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Container(
              width: 28,
              height: 28,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: active
                    ? SimfTokens.navy.withValues(alpha: 0.3)
                    : SimfTokens.navyDisabledBorder,
                borderRadius: BorderRadius.circular(5),
              ),
              child: Text(
                '$count',
                style: TextStyle(
                  color: active ? Colors.white : SimfTokens.accent,
                  fontWeight: FontWeight.w700,
                  fontSize: SimfTokens.textMd,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
