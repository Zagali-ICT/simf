import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/utils/gregorian_month_names.dart';
import '../../../core/utils/saudi_time.dart';
import '../data/notification_models.dart';
import 'notification_card.dart';

/// The day-grouped list: a "اليوم / أمس / date" header per run of same-day
/// items, then the cards for that day.
class NotificationGroupedList extends StatelessWidget {
  const NotificationGroupedList({
    required this.items,
    required this.isArabic,
    required this.l10n,
    required this.onTap,
    super.key,
  });

  final List<NotificationItem> items;
  final bool isArabic;
  final AppL10n l10n;
  final ValueChanged<NotificationItem> onTap;

  @override
  Widget build(BuildContext context) {
    // Build an ordered (dayLabel, items) list, preserving the newest-first
    // order the API returns.
    final groups = <MapEntry<String, List<NotificationItem>>>[];
    for (final item in items) {
      final label = _dayLabel(item.createdAt);
      if (groups.isNotEmpty && groups.last.key == label) {
        groups.last.value.add(item);
      } else {
        groups.add(MapEntry(label, <NotificationItem>[item]));
      }
    }
    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        0,
        SimfTokens.space4,
        SimfTokens.space4,
      ),
      children: <Widget>[
        for (final group in groups) ...<Widget>[
          if (group.key.isNotEmpty)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: SimfTokens.space2),
              child: Text(
                group.key,
                // Frame 758:2491 — 16px Medium, white.
                style: SimfTokens.labelWhiteMediumLg,
              ),
            ),
          for (final item in group.value)
            NotificationCard(
              item: item,
              isArabic: isArabic,
              dayLabel: group.key,
              onTap: () => onTap(item),
            ),
        ],
      ],
    );
  }

  String _dayLabel(DateTime? createdAt) {
    if (createdAt == null) {
      return '';
    }
    final local = saudiOf(createdAt);
    final now = saudiNow();
    final today = DateTime(now.year, now.month, now.day);
    final d = DateTime(local.year, local.month, local.day);
    final diff = today.difference(d).inDays;
    if (diff == 0) {
      return l10n.dayToday;
    }
    if (diff == 1) {
      return l10n.dayYesterday;
    }
    // Localised month name (Arabic on the ar UI) — never the intl English
    // fallback that showed "Jun 10" inside the RTL screen.
    return '${local.day} ${gregorianMonthName(local.month, isArabic)}';
  }
}
