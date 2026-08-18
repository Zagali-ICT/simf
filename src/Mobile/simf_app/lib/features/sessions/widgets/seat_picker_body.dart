import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/widgets/hall_seat_map.dart';
import 'package:simf_app/features/sessions/widgets/selected_seat_chip.dart';

/// The seat picker's loaded body: the session title + hint, the selectable hall
/// card, the confirmation chip, and the confirm / auto-pick CTAs.
///
/// The screen owns the selection, the endpoints and the confirm dialogs; this
/// owns the layout that reads them.
class SeatPickerBody extends StatelessWidget {
  const SeatPickerBody({
    required this.map,
    required this.held,
    required this.l10n,
    required this.busy,
    required this.selectedRow,
    required this.selectedSeat,
    required this.onSeatTap,
    required this.onConfirm,
    required this.onRandom,
    super.key,
  });

  final SessionSeatMap map;

  /// B1 — the seat the caller already holds, which puts the picker in CHANGE
  /// mode (its own title, hint and CTA, and no auto-pick). Null for an ordinary
  /// reserve.
  final SeatCell? held;

  final AppL10n l10n;
  final bool busy;
  final String? selectedRow;
  final int? selectedSeat;
  final void Function(String row, int seat) onSeatTap;
  final VoidCallback onConfirm;
  final VoidCallback onRandom;

  @override
  Widget build(BuildContext context) {
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
          Text(
            map.localizedSessionTitle(isArabic: l10n.isArabic) ??
                (held == null ? l10n.seatPickerTitle : l10n.seatChangeTitle),
            textAlign: TextAlign.center,
            style: SimfTokens.labelWhiteBoldTitle,
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            held == null ? l10n.seatPickerHint : l10n.seatChangeHint,
            textAlign: TextAlign.center,
            style: SimfTokens.labelBeigeSm,
          ),
          // B1 — in change mode, name the seat being left so the destination is
          // always chosen against a visible "from".
          if (held != null) ...<Widget>[
            const SizedBox(height: SimfTokens.space2),
            Text(
              l10n.seatLocation(held!.rowLabel, held!.seatNumber),
              textAlign: TextAlign.center,
              style: SimfTokens.labelWhiteSemibold,
            ),
          ],
          // D-771 — explain the padlocked seats, but only for a tiered hall so
          // a plain hall keeps the shipped copy unchanged.
          if (map.hasTiers) ...<Widget>[
            const SizedBox(height: SimfTokens.space2),
            Text(
              l10n.seatTierPickerHint,
              textAlign: TextAlign.center,
              style: SimfTokens.labelBeigeSm,
            ),
          ],
          const SizedBox(height: SimfTokens.space5),
          // The shared hall card in its selectable configuration: available
          // seats tappable with a gold border cue, 26px seat cap, 16px legend
          // swatches (the picker's pre-consolidation render, D-600). A tap
          // SELECTS (highlights) the seat; the Confirm CTA below commits it.
          HallSeatMapCard(
            map: map,
            l10n: l10n,
            busy: busy,
            onSeatTap: onSeatTap,
            selectedRowLabel: selectedRow,
            selectedSeatNumber: selectedSeat,
            maxSeatSize: SimfTokens.seatCapPicker,
            availableBorderColor: SimfTokens.accent,
            swatchSize: SimfTokens.seatSwatchLg,
          ),
          const SizedBox(height: SimfTokens.space5),
          if (selectedRow != null && selectedSeat != null) ...<Widget>[
            SelectedSeatChip(
              label: l10n.seatPickerSelectedChip(selectedRow!, selectedSeat!),
            ),
            const SizedBox(height: SimfTokens.space4),
          ],
          FilledButton.icon(
            onPressed: (busy || selectedRow == null || selectedSeat == null)
                ? null
                : onConfirm,
            style: FilledButton.styleFrom(
              minimumSize: const Size.fromHeight(SimfTokens.controlHeight),
              backgroundColor: SimfTokens.accent,
              foregroundColor: SimfTokens.surface,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              ),
            ),
            icon: const Icon(Icons.event_seat,
                size: SimfTokens.seatPickerScreenSize,),
            label: Text(
              held == null
                  ? l10n.seatPickerConfirmCta
                  : l10n.seatChangeConfirmCta,
              style: SimfTokens.titleBold,
            ),
          ),
          // B1 — no auto-pick when changing seats: a move is a deliberate
          // choice of WHERE to go, and a random destination would be a worse
          // seat lottery.
          if (held == null) ...<Widget>[
            const SizedBox(height: SimfTokens.space4),
            FilledButton.icon(
              onPressed: busy ? null : onRandom,
              style: FilledButton.styleFrom(
                minimumSize: const Size.fromHeight(SimfTokens.controlHeight),
                backgroundColor: SimfTokens.accent,
                foregroundColor: SimfTokens.surface,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                ),
              ),
              icon: const Icon(Icons.shuffle,
                  size: SimfTokens.seatPickerScreenSize,),
              label: Text(
                l10n.seatPickerRandomCta,
                style: SimfTokens.titleBold,
              ),
            ),
          ],
        ],
      ),
    );
  }
}
