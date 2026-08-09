import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/widgets/seat_chip.dart';

/// The "الجلسة" card (frame 905:1556): the session label, its title, then the
/// seat (مقعد) + row (الصف) chips — right-aligned on the navy `navyDeep` fill.
class SessionCard extends StatelessWidget {
  const SessionCard({required this.map, required this.l10n, super.key});

  final SessionSeatMap map;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final cell = map.myCell;
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            l10n.sessionLabel,
            textAlign: TextAlign.start,
            style: SimfTokens.labelBeigeSemiboldLg,
          ),
          const SizedBox(height: SimfTokens.space4),
          Text(
            // D-432 — the real session title now ships on the seat map; the
            // seat row/number are shown by the chips below. Fall back to the
            // seat location (or the no-seat hint) when the title is absent.
            map.localizedSessionTitle(l10n.isArabic) ??
                (cell != null
                    ? l10n.seatLocation(cell.rowLabel, cell.seatNumber)
                    : l10n.noSeatYet),
            textAlign: TextAlign.center,
            style: SimfTokens.labelWhiteSemiboldTitle,
          ),
          const SizedBox(height: SimfTokens.space4),
          Row(
            children: <Widget>[
              // RTL: the row (الصف) chip sits at the inline-start (right), the
              // seat (مقعد) chip at the inline-end (left) — frame 905:1576.
              Expanded(
                child: SeatChip(
                  goldLabel: l10n.rowChipLabel,
                  value: cell != null ? cell.rowLabel : '—',
                  borderColor: SimfTokens.accent,
                  borderWidth: SimfTokens.hairlineBold,
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: SeatChip(
                  goldLabel: l10n.seatChipLabel,
                  value: cell != null ? '${cell.seatNumber}' : '—',
                  borderColor: SimfTokens.beigeBorder,
                  borderWidth: SimfTokens.hairline,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
