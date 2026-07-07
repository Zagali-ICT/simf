import 'package:flutter/material.dart';
import 'package:intl/intl.dart' show DateFormat;

import '../../../app/theme/tokens.dart';
import '../../../core/utils/weekday_names.dart';
import '../../sessions/data/session_models.dart';

/// 24h "HH:mm" for the session sub-line; 12h "hh:mm a" for the agenda rows
/// (locale-data-free, pinned 'en' for Western digits as the frame shows).
final DateFormat _time24 = DateFormat('HH:mm', 'en');
final DateFormat _agendaTime = DateFormat('hh:mm a', 'en');

/// The "الجلسة" info card (frame 1072:14628): the white Medium label over the
/// bordered selected-session box (gold title + day·time·duration·hall sub-line),
/// then that day's agenda timeline.
class SummarySessionCard extends StatelessWidget {
  const SummarySessionCard({
    required this.label,
    required this.session,
    required this.agenda,
    required this.isArabic,
    required this.durationLabel,
    super.key,
  });

  final String label;
  final SessionListItem session;
  final List<SessionListItem> agenda;
  final bool isArabic;
  final String durationLabel;

  @override
  Widget build(BuildContext context) {
    final hall = session.localizedHall(isArabic);
    final sub = <String>[
      gregorianWeekdayName(session.startLocal, isArabic),
      _time24.format(session.startLocal),
      durationLabel,
      if (hall != null && hall.trim().isNotEmpty) hall,
    ].join(' · ');
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
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const SizedBox(height: SimfTokens.space2),
          Text(
            label,
            // start = right in Arabic (RTL), left in English — matches the
            // frame (right-aligned) and adapts to the language (was .end,
            // which renders LEFT in RTL).
            textAlign: TextAlign.start,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textLg,
              fontWeight: FontWeight.w500,
            ),
          ),
          const SizedBox(height: SimfTokens.space4),
          // The bordered selected-session box (frame 1072:14603).
          Container(
            padding: const EdgeInsets.symmetric(
              horizontal: SimfTokens.space2,
              vertical: SimfTokens.space4,
            ),
            decoration: BoxDecoration(
              border: Border.all(
                color: SimfTokens.beigeBorder,
                width: SimfTokens.hairline,
              ),
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            ),
            child: Column(
              // start = leading edge (right in RTL / left in LTR) — the frame
              // right-aligns the title + sub in Arabic; .end rendered LEFT.
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  session.localizedTitle(isArabic),
                  textAlign: TextAlign.start,
                  style: const TextStyle(
                    color: SimfTokens.accent,
                    fontSize: SimfTokens.textLg,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: SimfTokens.space2),
                Text(
                  sub,
                  textAlign: TextAlign.start,
                  style: const TextStyle(
                    color: SimfTokens.beigeBorder,
                    fontSize: SimfTokens.textSm,
                  ),
                ),
              ],
            ),
          ),
          if (agenda.isNotEmpty) ...<Widget>[
            const SizedBox(height: SimfTokens.space2),
            for (final item in agenda)
              SummaryAgendaRow(item: item, isArabic: isArabic),
          ],
        ],
      ),
    );
  }
}

/// One day-agenda row (frame 1072:14612): the agenda title at the inline-start
/// (right under RTL) and the 12h time at the inline-end (left).
class SummaryAgendaRow extends StatelessWidget {
  const SummaryAgendaRow({
    required this.item,
    required this.isArabic,
    super.key,
  });

  final SessionListItem item;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space3,
      ),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(
              item.localizedTitle(isArabic),
              textAlign: TextAlign.start,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: Colors.white,
                fontSize: SimfTokens.textMd,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
          const SizedBox(width: SimfTokens.space3),
          Text(
            _agendaTime.format(item.startLocal),
            textDirection: TextDirection.ltr,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}
