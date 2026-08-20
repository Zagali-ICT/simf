import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/utils/gregorian_month_names.dart';
import 'package:simf_app/core/utils/group_consecutive.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/notifications/data/notification_models.dart';
import 'package:simf_app/features/notifications/widgets/notification_card.dart';

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
    // Runs, not buckets: the API returns newest-first, so "Today" heads the
    // list once rather than collecting every Today row from the whole history.
    final groups = groupConsecutive(items, (i) => _dayLabel(i.createdAt));
    // Flattened to one row per line so the feed builds lazily: a row whose item
    // is null is that run's day header, and every row carries the day label the
    // card stamps its time with.
    final rows = <(String, NotificationItem?)>[];
    for (final group in groups) {
      if (group.key.isNotEmpty) {
        rows.add((group.key, null));
      }
      for (final item in group.value) {
        rows.add((group.key, item));
      }
    }
    return ListView.builder(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        0,
        SimfTokens.space4,
        SimfTokens.space4,
      ),
      itemCount: rows.length,
      itemBuilder: (context, index) {
        final (dayLabel, item) = rows[index];
        if (item == null) {
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: SimfTokens.space2),
            child: Text(
              dayLabel,
              // Frame 758:2491 — 16px Medium, white.
              style: SimfTokens.labelWhiteMediumLg,
            ),
          );
        }
        return NotificationCard(
          key: ValueKey<String>(item.id),
          item: item,
          isArabic: isArabic,
          dayLabel: dayLabel,
          onTap: () => onTap(item),
        );
      },
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
    final month = gregorianMonthName(local.month, isArabic: isArabic);
    return '${local.day} $month';
  }
}
