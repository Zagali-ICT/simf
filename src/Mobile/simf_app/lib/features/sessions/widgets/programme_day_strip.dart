import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/widgets/day_cell.dart';

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
          const gap = SimfTokens.space1;
          // Comfortable per-day width (owner 2026-06-30 — the strip was
          // cramped); when the full date range exceeds the width the band
          // scrolls horizontally rather than squeezing every day.
          const minCell = SimfTokens.dayStripCellWidth;
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

  Widget _cell(_CalendarDay e) => DayCell(
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

/// One day cell in the programme calendar strip, with the weekday label it
/// renders. One date in the agenda calendar strip: [programmeDay] is null for
/// an empty in-between day (no sessions), set for a day that carries sessions.
class _CalendarDay {
  const _CalendarDay(this.date, this.programmeDay);

  final DateTime date;
  final ProgrammeDay? programmeDay;
}
