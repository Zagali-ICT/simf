import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../data/session_models.dart';
import 'programme_day_banner.dart';

/// One المواعيد timeline row (frame 1310:3213 collapsed / 1310:3232 featured):
/// a navy radius-8 card holding, inline-start→end, the content column (a 14px
/// gold calendar icon + the gold right-aligned title, the day banner on the
/// featured row, then the grey description) and a trailing vertical **time rail**
/// (start time over a hairline connector over the end time). No leading number,
/// no trailing chevron (the updated frame dropped both). The whole row taps
/// through to the session detail.
class SessionTimelineRow extends StatelessWidget {
  const SessionTimelineRow({
    required this.session,
    required this.isArabic,
    required this.onTap,
    this.featuredImageUrl,
    super.key,
  });

  final SessionListItem session;
  final bool isArabic;
  final VoidCallback onTap;
  final String? featuredImageUrl;

  @override
  Widget build(BuildContext context) {
    final description = session.localizedDescription(isArabic);
    final descriptionText = description == null
        ? null
        : Text(
            description,
            textAlign: TextAlign.start,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
              height: 1.4,
            ),
          );

    return SimfCard(
      onTap: onTap,
      // Frame 1310:3213 — radius 8, navy fill, no visible border.
      radius: SimfTokens.radius,
      borderWidth: 0,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space4,
          vertical: SimfTokens.space3,
        ),
        // IntrinsicHeight lets the leading time rail's connector stretch to the
        // full row height (title → description / banner).
        child: IntrinsicHeight(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              // Frame 883:2308 — the start/end time rail LEADS (inline-start =
              // physical right under RTL), then a full-height beige hairline
              // (Figma 1310:3239 / 1313:3355 — beige #C2B8A2 @ 40%) divides it
              // from the content, with an 8px gap each side (gap-[8px]).
              _TimeRail(start: session.startLocal, end: session.endLocal),
              const SizedBox(width: SimfTokens.space2),
              const SizedBox(
                width: 1,
                child: ColoredBox(color: SimfTokens.beigeBorder40),
              ),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    // Title line (Figma 1310:3215): the gold right-aligned title
                    // LEADS at the inline-start (physical right); the 14px gold
                    // calendar glyph TRAILS at the inline-end (physical left,
                    // next to the divider) — owner 2026-06-30.
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Expanded(
                          child: Text(
                            session.localizedTitle(isArabic),
                            textAlign: TextAlign.start,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: SimfTokens.accent,
                              fontSize: SimfTokens.textMd,
                              fontWeight: FontWeight.w500,
                              height: 1.3,
                            ),
                          ),
                        ),
                        const SizedBox(width: SimfTokens.space2),
                        const Padding(
                          padding: EdgeInsets.only(top: 1),
                          child: Icon(
                            Icons.calendar_today_outlined,
                            size: 14,
                            color: SimfTokens.accent,
                          ),
                        ),
                      ],
                    ),
                    if (featuredImageUrl != null) ...<Widget>[
                      const SizedBox(height: SimfTokens.space3),
                      ProgrammeDayBanner(imageUrl: featuredImageUrl),
                    ],
                    if (descriptionText != null) ...<Widget>[
                      const SizedBox(height: SimfTokens.space2),
                      descriptionText,
                    ],
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The trailing vertical time rail (frame 1310:3241): the start time at the top
/// (beige SemiBold), a thin beige connector down the middle, and the end time at
/// the bottom (white). Clock values stay LTR. Sized to fit "HH:MM" on one line,
/// full row height.
class _TimeRail extends StatelessWidget {
  const _TimeRail({required this.start, required this.end});

  final DateTime start;
  final DateTime end;

  static String _hhmm(DateTime t) =>
      '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}';

  @override
  Widget build(BuildContext context) {
    // The rail is sized to fit "HH:MM" on ONE line (owner 2026-06-30 — the 34px
    // rail wrapped the time into two rows); the row's content gets the rest of
    // the width via Expanded. The timeline reads from (top) → connector line →
    // to (bottom).
    return SizedBox(
      width: 48,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: <Widget>[
          Text(
            _hhmm(start),
            textDirection: TextDirection.ltr,
            maxLines: 1,
            softWrap: false,
            overflow: TextOverflow.visible,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w600,
            ),
          ),
          const Expanded(
            child: Padding(
              padding: EdgeInsets.symmetric(vertical: SimfTokens.space1),
              child: SizedBox(
                width: 1,
                // Figma 1310:3244 — the in-rail "from → to" connector is SOLID
                // beige (#C2B8A2), the timeline feel the owner asked for. (The
                // faint 40% line is the separate content/rail divider.)
                child: ColoredBox(color: SimfTokens.beigeBorder),
              ),
            ),
          ),
          Text(
            _hhmm(end),
            textDirection: TextDirection.ltr,
            maxLines: 1,
            softWrap: false,
            overflow: TextOverflow.visible,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w400,
            ),
          ),
        ],
      ),
    );
  }
}
