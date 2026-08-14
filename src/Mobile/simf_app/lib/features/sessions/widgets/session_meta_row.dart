import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/utils/gregorian_month_names.dart';
import 'package:simf_app/core/utils/weekday_names.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/widgets/meta_item.dart';

/// The icon + label meta row on the session header, with its time formatter.
/// Interim local time format: `HH:MM` (device-local). Final locale-aware
/// formatting lands with the designer pass (SIMF-VID-001).
String _time(DateTime local) {
  final hour = local.hour.toString().padLeft(2, '0');
  final minute = local.minute.toString().padLeft(2, '0');
  return '$hour:$minute';
}

/// The meta line (frame 889:2698): a clock + time range, a separator dot, and a
/// calendar + weekday/day, in beige paragraph text. Laid **LTR** so each segment
/// is icon→text (icon leads at the left, owner 2026-06-30) and the time reads
/// "09:00 — 10:30" start→end; the time segment sits left, the date segment right
/// (matching the frame). The Arabic date still renders RTL within its own box.
class SessionMetaRow extends StatelessWidget {
  const SessionMetaRow({required this.detail, required this.isArabic, super.key});

  final SessionDetail detail;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final start = detail.startLocal;
    final end = detail.endLocal;
    return Directionality(
      textDirection: TextDirection.ltr,
      child: Wrap(
        crossAxisAlignment: WrapCrossAlignment.center,
        spacing: SimfTokens.space3,
        runSpacing: SimfTokens.space1,
        children: <Widget>[
          MetaItem(
            icon: Icons.schedule_outlined,
            label: '${_time(start)} — ${_time(end)}',
          ),
          const Text(
            '·',
            // Frame 889:2702 — the separator dot is white (#FFFFFF), heavier than
            // the beige time/date items beside it.
            style: SimfTokens.labelWhiteBlackLg,
          ),
          MetaItem(
            icon: Icons.calendar_today_outlined,
            label: '${gregorianWeekdayName(start, isArabic: isArabic)} · '
                '${start.day.toString().padLeft(2, '0')} '
                '${gregorianMonthName(start.month, isArabic: isArabic)}',
          ),
        ],
      ),
    );
  }
}
