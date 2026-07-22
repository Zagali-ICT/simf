import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/app_assets.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../../../app/widgets/simf_svg_icon.dart';
import '../data/seat_map_models.dart';

/// D-485 — the reservation card: the held seat (الصف · مقعد) or "general
/// admission" for an open-seating join, plus the pending-approval hint. A
/// seat-specific booking is tappable to open the seat map (18); an open-seating
/// join has no seat to view, so the card is inert (no chevron). The cancel
/// action is a separate white line below the CTA row (owner 2026-06-30), so the
/// card carries no Cancel itself.
class SessionReservationCard extends StatelessWidget {
  const SessionReservationCard({
    required this.cell,
    required this.l10n,
    this.onView,
    super.key,
  });

  final SeatCell cell;
  final AppL10n l10n;
  final VoidCallback? onView;

  @override
  Widget build(BuildContext context) {
    final isOpen = cell.kind == SeatReservationKind.openSeating;
    final title = isOpen
        ? l10n.generalAdmissionLabel
        : l10n.seatLocation(cell.rowLabel, cell.seatNumber);
    // D-572 — once the booking is approved the card swaps the pending line for
    // the "show your badge at entry" hint (Figma 889:2766); otherwise it stays
    // "awaiting approval".
    final hint = cell.status == BookingStatus.approved
        ? l10n.seatShowBadgeHint
        : l10n.reservationPendingHint;
    return SimfCard(
      onTap: onView,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Row(
          children: <Widget>[
            Semantics(
              button: onView != null,
              label: l10n.seatViewLink,
              child: const _SeatMarker(),
            ),
            const SizedBox(width: SimfTokens.space4),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    title,
                    style: SimfTokens.labelWhiteSemiboldLg,
                  ),
                  const SizedBox(height: SimfTokens.space2),
                  Text(
                    hint,
                    style: SimfTokens.labelBeigeSm,
                  ),
                ],
              ),
            ),
            if (onView != null) ...<Widget>[
              const SizedBox(width: SimfTokens.space2),
              // Figma 889:2762 — the my-seat arrow is a thin STROKED chevron
              // (left-pointing "‹"), NOT the filled triangle of ic_caret_left.
              // ic_back.svg is that stroked chevron; SimfSvgIcon never mirrors,
              // so it stays left-pointing in RTL (same fix as speakers_screen).
              const SimfSvgIcon(
                AppAssets.icBack,
                size: 20,
                color: SimfTokens.beigeBorder,
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// The gold filled marker box on the my-seat card (frame 894:2779): a 44×44
/// gold-bordered tile wrapping a small gold filled square.
class _SeatMarker extends StatelessWidget {
  const _SeatMarker();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: SimfTokens.tapTarget,
      height: SimfTokens.tapTarget,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent.withValues(alpha: SimfTokens.seatFillOpacity),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(color: SimfTokens.accent),
      ),
      child: Container(
        width: SimfTokens.seatMarkerInner,
        height: SimfTokens.seatMarkerInner,
        decoration: BoxDecoration(
          color: SimfTokens.accent,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        ),
      ),
    );
  }
}
