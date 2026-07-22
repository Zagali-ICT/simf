import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../data/session_models.dart';

/// The agenda day strip (frame node 883:2327, restyled #4): a **white**
/// calendar band that shows the programme days **plus muted neighbour days
/// before and after** ([_padDays] greyed, non-selectable days on each side),
/// so the event reads centred with its neighbours peeking in
/// (SAT 12 · SUN 13 · [MON 14 · TUE 15 · WED 16] · THU 17 · FRI 18). Each cell
/// shows the weekday over the centred day number. A day **with** sessions is
/// "active"; the **selected** day is a navy pill; a padding / empty in-between
/// day is muted grey and not selectable. When the band fits the width the cells
/// distribute (event centred, as on a tablet); when it doesn't (e.g. a narrow
/// phone once the pad days are added) it scrolls horizontally from the leading
/// day. The cell **order** follows the ambient text direction — left→right in
/// English, and right→left in Arabic (earliest programme day on the right), the
/// scroll leading from the right edge (owner 2026-07-22).
class ProgrammeDayStrip extends StatelessWidget {
  const ProgrammeDayStrip({
    required this.days,
    required this.selectedId,
    required this.onChanged,
    super.key,
  });

  /// Muted neighbour days shown on each side of the programme (Figma 883:2327
  /// pads the event with 2 greyed days before the first and 2 after the last).
  static const int _padDays = 2;

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
      // The band inherits the AMBIENT text direction (owner 2026-07-22): in
      // Arabic the agenda page is RTL, so the day cells order right→left — the
      // earliest programme day on the right, the latest on the left — and the
      // horizontal scroll leads from the right edge; in English (LTR) the cells
      // stay left→right (SAT 12 … FRI 18) exactly as before. This OVERRIDES the
      // earlier 883:2327 LTR pin, which forced the strip left→right in every
      // locale ("like the times"). The weekday labels stay English 3-letter.
      child: _buildBand(entries),
    );
  }

  Widget _buildBand(List<_CalendarDay> entries) {
    return LayoutBuilder(
      builder: (context, constraints) {
          const double gap = SimfTokens.space1;
          // Comfortable per-day width (owner 2026-06-30 — the strip was cramped);
          // when the full date range exceeds the width the band scrolls
          // horizontally rather than squeezing every day.
          const double minCell = SimfTokens.dayStripCellWidth;
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

  /// The contiguous date range for the calendar band, matching Figma 883:2327:
  /// the programme days (first→last, inclusive) plus [_padDays] muted days on
  /// each side, so the event is centred with greyed neighbour days around it.
  /// Each date maps to its [ProgrammeDay] when one exists (an "active" day) or
  /// null (a padding / empty in-between day).
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
    // Pad the event with muted neighbour days on each side (Figma 883:2327).
    final start = DateTime(first!.year, first.month, first.day - _padDays);
    final end = DateTime(last!.year, last.month, last.day + _padDays);
    final out = <_CalendarDay>[];
    var cur = start;
    while (!cur.isAfter(end)) {
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
    // white text; a day with sessions shows navy text (no fill); a padding /
    // empty day is uniformly muted grey (#C2C2C2). The weekend-red weekday
    // accent applies only to active (session) days — the frame shows the greyed
    // neighbour days (incl. SAT/SUN) with a plain grey label.
    final bool isWeekend =
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

/// A short 3-letter English weekday for the day strip (LTR, as in the frame).
String _weekdayEn(DateTime day) {
  const names = <String>['MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT', 'SUN'];
  return names[day.weekday - 1];
}
