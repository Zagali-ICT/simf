import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../data/session_models.dart';

/// The agenda day strip (frame node 883:2327, restyled #4): a **white** calendar
/// band spanning the **full** event date range — every day from the first to the
/// last programme day, not only the days that carry sessions. Each cell shows the
/// weekday over the centred day number. A day **with** sessions is "active"
/// (white); the **selected** day is navy; an empty in-between day is muted grey
/// and not selectable. The strip fills the width (cells distributed), falling
/// back to a horizontal scroll when the range is too long to fit.
class ProgrammeDayStrip extends StatelessWidget {
  const ProgrammeDayStrip({
    required this.days,
    required this.selectedId,
    required this.onChanged,
    super.key,
  });

  final List<ProgrammeDay> days;
  final String selectedId;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    final entries = _calendarRange(days);
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: const BoxDecoration(
        // Frame 883:2327 — the day strip is a WHITE band (the old grey
        // calendarBand was the superseded design).
        color: SimfTokens.surface,
        borderRadius:
            BorderRadius.all(Radius.circular(SimfTokens.radiusSmall)),
      ),
      // The frame render lays the calendar band LTR — dates ascend
      // left→right (SAT 12 … FRI 18) with English weekday labels — even on
      // the RTL page, so the strip pins its own direction (like the times).
      child: Directionality(
        textDirection: TextDirection.ltr,
        child: _buildBand(entries),
      ),
    );
  }

  Widget _buildBand(List<_CalendarDay> entries) {
    return LayoutBuilder(
      builder: (context, constraints) {
          const double gap = SimfTokens.space1;
          // Comfortable per-day width (owner 2026-06-30 — the strip was cramped);
          // when the full date range exceeds the width the band scrolls
          // horizontally rather than squeezing every day.
          const double minCell = 52;
          final width = entries.length * minCell + (entries.length - 1) * gap;
          // Guard the unbounded-width case (e.g. if ever placed outside a
          // width-bounded parent) so the Expanded row can't throw.
          final fits =
              constraints.maxWidth.isFinite && width <= constraints.maxWidth;
          final row = Row(
            mainAxisSize: fits ? MainAxisSize.max : MainAxisSize.min,
            children: <Widget>[
              for (var i = 0; i < entries.length; i++) ...<Widget>[
                if (i > 0) const SizedBox(width: gap),
                if (fits)
                  Expanded(child: _cell(entries[i]))
                else
                  SizedBox(width: minCell, child: _cell(entries[i])),
              ],
            ],
          );
          return fits
              ? row
              : SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: row,
                );
      },
    );
  }

  Widget _cell(_CalendarDay e) => _DayCell(
        date: e.date,
        hasSessions: e.programmeDay != null,
        selected: e.programmeDay?.id == selectedId,
        onTap:
            e.programmeDay == null ? null : () => onChanged(e.programmeDay!.id),
      );

  /// The contiguous date range from the programme days: span the first to the
  /// last day (inclusive), mapping each date to its [ProgrammeDay] when one
  /// exists (an "active" day) or null (an empty in-between day).
  static List<_CalendarDay> _calendarRange(List<ProgrammeDay> days) {
    if (days.isEmpty) {
      return const <_CalendarDay>[];
    }
    final byDate = <DateTime, ProgrammeDay>{};
    DateTime? first;
    DateTime? last;
    for (final d in days) {
      final date = DateTime(d.date.year, d.date.month, d.date.day);
      byDate[date] = d;
      if (first == null || date.isBefore(first)) {
        first = date;
      }
      if (last == null || date.isAfter(last)) {
        last = date;
      }
    }
    final out = <_CalendarDay>[];
    var cur = first!;
    while (!cur.isAfter(last!)) {
      out.add(_CalendarDay(cur, byDate[cur]));
      cur = DateTime(cur.year, cur.month, cur.day + 1);
    }
    return out;
  }
}

/// One date in the agenda calendar strip: [programmeDay] is null for an empty
/// in-between day (no sessions), set for a day that carries sessions.
class _CalendarDay {
  const _CalendarDay(this.date, this.programmeDay);

  final DateTime date;
  final ProgrammeDay? programmeDay;
}

class _DayCell extends StatelessWidget {
  const _DayCell({
    required this.date,
    required this.hasSessions,
    required this.selected,
    required this.onTap,
  });

  final DateTime date;
  final bool hasSessions;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    // Frame 883:2327 — on the white band: the selected day is a navy pill with
    // white text; a day with sessions shows navy text (no fill); an empty
    // in-between day is muted grey (#C2C2C2). Weekend weekday labels (SAT/SUN)
    // render red on non-selected days.
    final bool isWeekend =
        date.weekday == DateTime.saturday || date.weekday == DateTime.sunday;
    final Color fill;
    final Color numberColor;
    final Color weekdayColor;
    if (selected) {
      fill = SimfTokens.navy;
      numberColor = Colors.white;
      weekdayColor = Colors.white;
    } else if (hasSessions) {
      fill = Colors.transparent;
      numberColor = SimfTokens.navy;
      weekdayColor = isWeekend ? SimfTokens.danger : SimfTokens.navy;
    } else {
      fill = Colors.transparent;
      numberColor = SimfTokens.dayInactive;
      weekdayColor = isWeekend ? SimfTokens.danger : SimfTokens.dayInactive;
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

/// A short 3-letter English weekday for the day strip (LTR, as in the frame).
String _weekdayEn(DateTime day) {
  const names = <String>['MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT', 'SUN'];
  return names[day.weekday - 1];
}
