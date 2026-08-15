import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// A short 3-letter English weekday for the day strip (LTR, as in the frame).
String _weekdayEn(DateTime day) {
  const names = <String>['MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT', 'SUN'];
  return names[day.weekday - 1];
}

class DayCell extends StatelessWidget {
  const DayCell({
    required this.date,
    required this.hasSessions,
    required this.selected,
    required this.onTap,
    super.key,
  });

  final DateTime date;
  final bool hasSessions;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    // Frame 883:2327 — on the white band: the selected day is a navy pill with
    // white text; a day with sessions shows navy text (no fill); a padding /
    // empty day is uniformly muted grey (#C2C2C2). The weekend-red weekday
    // accent applies only to active (session) days — the frame shows the greyed
    // neighbour days (incl. SAT/SUN) with a plain grey label.
    final isWeekend =
        date.weekday == DateTime.saturday || date.weekday == DateTime.sunday;
    final Color fill;
    final Color numberColor;
    final Color weekdayColor;
    if (selected) {
      fill = SimfTokens.navy;
      numberColor = SimfTokens.surface;
      weekdayColor = SimfTokens.surface;
    } else if (hasSessions) {
      fill = SimfTokens.transparent;
      numberColor = SimfTokens.navy;
      weekdayColor = isWeekend ? SimfTokens.danger : SimfTokens.navy;
    } else {
      fill = SimfTokens.transparent;
      numberColor = SimfTokens.dayInactive;
      weekdayColor = SimfTokens.dayInactive;
    }
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: Container(
        // Figma day-picker cell 883:2328 — px-8 / py-4.
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space2,
          vertical: SimfTokens.space1,
        ),
        decoration: BoxDecoration(
          color: fill,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              _weekdayEn(date),
              style: TextStyle(
                color: weekdayColor,
                fontSize: SimfTokens.textXs,
                fontWeight: FontWeight.w600,
              ),
            ),
            Text(
              date.day.toString(),
              textAlign: TextAlign.center,
              style: TextStyle(
                color: numberColor,
                fontSize: SimfTokens.textMd,
                // Frame 883:2331 "Subheadline/semibold 14" — SemiBold, not bold.
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
