import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/widgets/change_seat_button.dart';
import 'package:simf_app/features/sessions/widgets/hall_seat_map.dart';
import 'package:simf_app/features/sessions/widgets/session_card.dart';
import 'package:simf_app/features/sessions/widgets/sessions_actions.dart';

class SeatMapView extends StatelessWidget {
  const SeatMapView({
    required this.map,
    required this.l10n,
    required this.onNavigate,
    this.onShare,
    this.onChangeSeat,
  });

  final SessionSeatMap map;
  final AppL10n l10n;
  final VoidCallback onNavigate;
  final VoidCallback? onShare;

  /// B1 — null when there is no seat-specific hold to move.
  final VoidCallback? onChangeSeat;

  @override
  Widget build(BuildContext context) {
    // Arabic is primary; the whole page reads right-to-left (frame 898:2873).
    return Directionality(
      textDirection: TextDirection.rtl,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space2,
          SimfTokens.space4,
          SimfTokens.space5,
        ),
        children: <Widget>[
          SessionCard(map: map, l10n: l10n),
          // Frame gaps: session card (ends y265) → hall card (y289) = 24; hall
          // card (ends y699) → actions (y739) = 40 (no 40 in the spacing scale).
          const SizedBox(height: SimfTokens.space6),
          // Read-only defaults = this frame (898:2873): beige available
          // border, 20px seat cap, 14px reserved/mine swatches.
          HallSeatMapCard(map: map, l10n: l10n),
          const SizedBox(height: SimfTokens.space10),
          SessionsActions(l10n: l10n, onNavigate: onNavigate, onShare: onShare),
          // B1 — the change-seat CTA sits on its own full-width line below the
          // frame's two actions, so the shipped 908:1733 row is untouched.
          if (onChangeSeat != null) ...<Widget>[
            const SizedBox(height: SimfTokens.space4),
            ChangeSeatButton(l10n: l10n, onChangeSeat: onChangeSeat!),
          ],
        ],
      ),
    );
  }
}
