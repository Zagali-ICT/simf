import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// The left time column of a timeline row, with its HH:mm formatter.
/// The trailing vertical time rail (frame 1310:3241): the start time at the top
/// (beige SemiBold), a thin beige connector down the middle, and the end time at
/// the bottom (white). Clock values stay LTR. Sized to fit "HH:MM" on one line,
/// full row height.
class SessionTimeRail extends StatelessWidget {
  const SessionTimeRail({required this.start, required this.end});

  final DateTime start;
  final DateTime end;

  /// Floor for the rail height so the from→to connector is always visible. The
  /// connector is an [Expanded] line, so on a short row (a title-only session,
  /// no description/banner) it collapses to zero — the "line missing between from
  /// and to time" the owner reported. This floor (two ~15px time labels + a ~14px
  /// connector) keeps it drawn; taller rows let it stretch to fill.
  static const double _minRailHeight = SimfTokens.timeRailMinHeight;

  static String _hhmm(DateTime t) =>
      '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}';

  @override
  Widget build(BuildContext context) {
    // The rail is sized to fit "HH:MM" on ONE line (owner 2026-06-30 — the 34px
    // rail wrapped the time into two rows); the row's content gets the rest of
    // the width via Expanded. The timeline reads from (top) → connector line →
    // to (bottom).
    return ConstrainedBox(
      constraints: const BoxConstraints(minHeight: _minRailHeight),
      child: SizedBox(
        width: SimfTokens.timeRailWidth,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.center,
          children: <Widget>[
            Text(
              _hhmm(start),
              textDirection: TextDirection.ltr,
              maxLines: 1,
              softWrap: false,
              overflow: TextOverflow.visible,
              style: SimfTokens.labelBeigeSemiboldSm,
            ),
            const Expanded(
              // Figma 1310:3243/3244 — the in-rail "from → to" connector is a
              // SOLID 1px beige (#C2B8A2) line touching the two times directly
              // (no vertical gap in the frame). (The faint 40% line is the
              // separate content/rail divider.)
              child: SizedBox(
                width: 1,
                child: ColoredBox(color: SimfTokens.beigeBorder),
              ),
            ),
            Text(
              _hhmm(end),
              textDirection: TextDirection.ltr,
              maxLines: 1,
              softWrap: false,
              overflow: TextOverflow.visible,
              style: SimfTokens.bodyWhiteRegularSm,
            ),
          ],
        ),
      ),
    );
  }
}
