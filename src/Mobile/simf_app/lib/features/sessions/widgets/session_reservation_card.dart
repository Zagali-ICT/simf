import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';
import 'package:simf_app/features/sessions/data/seat_enums.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/widgets/seat_marker.dart';

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
              child: const SeatMarker(),
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
                size: SimfTokens.sessionReservationCardSize,
                color: SimfTokens.beigeBorder,
              ),
            ],
          ],
        ),
      ),
    );
  }
}
