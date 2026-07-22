import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/seat_map_models.dart';

/// The hall seat-map card shared by **My-Seat** (Figma 898:2873 — read-only)
/// and the **Seat-Picker** (D-485 — selectable): the gold-bordered stage band,
/// the LTR A–H seat grid, and the محجوز / متاح / مقعدك legend on the navyDeep
/// fill. One reusable component configured per screen (owner 2026-07-03):
///
/// * [onSeatTap] — null renders the read-only My-Seat card; set, **available**
///   seats become tappable (reserved / own seats stay inert) and carry a
///   `rowLabel + seatNumber` Semantics button label.
/// * [busy] — freezes the taps while a reserve call is in flight.
/// * [maxSeatSize] — seats are squares that shrink to fit the width but never
///   grow past this cap (frame 902:1406 ≈ 20; the picker renders 26).
/// * [availableBorderColor] — beige on My-Seat (the frame), gold on the picker
///   as the tappable cue; the legend's available swatch follows it.
/// * [swatchSize] — the reserved/mine legend swatches (14 on My-Seat per the
///   frame, 16 on the picker); the available swatch is always 16.
class HallSeatMapCard extends StatelessWidget {
  const HallSeatMapCard({
    required this.map,
    required this.l10n,
    this.onSeatTap,
    this.busy = false,
    this.maxSeatSize = SimfTokens.seatCapDefault,
    this.availableBorderColor = SimfTokens.beigeBorder,
    this.swatchSize = SimfTokens.seatSwatchSm,
    super.key,
  });

  final SessionSeatMap map;
  final AppL10n l10n;
  final void Function(String rowLabel, int seatNumber)? onSeatTap;
  final bool busy;
  final double maxSeatSize;
  final Color availableBorderColor;
  final double swatchSize;

  @override
  Widget build(BuildContext context) {
    final reserved = map.reservedKeys();
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        children: <Widget>[
          _StageBar(label: l10n.stageLabelBilingual),
          const SizedBox(height: SimfTokens.space6),
          // The hall plan keeps the stage at the top and seat columns in venue
          // order — do not mirror the grid geometry in RTL (L-7). The seats are
          // square and sized to the width (see _SeatGridRow), centred by this
          // Column, so the full row is always visible — no horizontal scroll.
          Directionality(
            textDirection: TextDirection.ltr,
            child: Column(
              children: <Widget>[
                for (final (index, row) in map.rowLabels.indexed) ...<Widget>[
                  if (index > 0) const SizedBox(height: SimfTokens.space4),
                  _SeatGridRow(
                    rowLabel: row,
                    seatsPerRow: map.seatsPerRow,
                    reserved: reserved,
                    map: map,
                    maxSeatSize: maxSeatSize,
                    availableBorderColor: availableBorderColor,
                    onSeatTap: busy ? null : onSeatTap,
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(height: SimfTokens.space6),
          _Legend(
            l10n: l10n,
            availableBorderColor: availableBorderColor,
            swatchSize: swatchSize,
          ),
        ],
      ),
    );
  }
}

/// The gold-bordered "المسرح · STAGE" band at the top of the hall card
/// (frame 905:1584): a full-width navyDeep pill, gold hairline, gold label.
class _StageBar extends StatelessWidget {
  const _StageBar({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      height: SimfTokens.controlHeight,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        border: Border.all(
          color: SimfTokens.accent,
          width: SimfTokens.hairlineBold,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        label,
        textAlign: TextAlign.center,
        style: SimfTokens.bodyGold,
      ),
    );
  }
}

class _SeatGridRow extends StatelessWidget {
  const _SeatGridRow({
    required this.rowLabel,
    required this.seatsPerRow,
    required this.reserved,
    required this.map,
    required this.maxSeatSize,
    required this.availableBorderColor,
    this.onSeatTap,
  });

  final String rowLabel;
  final int seatsPerRow;
  final Set<String> reserved;
  final SessionSeatMap map;
  final double maxSeatSize;
  final Color availableBorderColor;
  final void Function(String rowLabel, int seatNumber)? onSeatTap;

  @override
  Widget build(BuildContext context) {
    // `hasLayout` (the caller) already rejects an empty hall; localise that
    // invariant here so the seat-size math below can never divide by zero.
    assert(seatsPerRow > 0, 'seatsPerRow must be > 0 (guarded by hasLayout)');
    // Seats are SQUARES (frame 902:1406). They shrink to fit a narrow (phone)
    // width but never stretch into wide rectangles on a tablet: each is capped
    // at [maxSeatSize] and the whole row is sized to content + centred by the
    // grid Column. Frame 902:1402 = row letter (12px box) + 8px.
    return LayoutBuilder(
      builder: (context, constraints) {
        const labelWidth = SimfTokens.seatRowLabelWidth;
        const seatGap = SimfTokens.gap6;
        final seatsArea = constraints.maxWidth - labelWidth - SimfTokens.space2;
        final fit = (seatsArea - seatGap * (seatsPerRow - 1)) / seatsPerRow;
        final seat = fit.clamp(0.0, maxSeatSize).toDouble();
        return Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            SizedBox(
              width: labelWidth,
              child: Text(
                rowLabel,
                textAlign: TextAlign.center,
                style: SimfTokens.labelBeigeSm,
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            for (var s = 1; s <= seatsPerRow; s++) ...<Widget>[
              if (s > 1) const SizedBox(width: seatGap),
              _SeatBox(
                size: seat,
                status: map.isMine(rowLabel, s)
                    ? _SeatStatus.mine
                    : reserved.contains('$rowLabel:$s')
                        ? _SeatStatus.reserved
                        : _SeatStatus.available,
                availableBorderColor: availableBorderColor,
                semanticsLabel: '$rowLabel$s',
                onTap: onSeatTap == null ? null : () => onSeatTap!(rowLabel, s),
              ),
            ],
          ],
        );
      },
    );
  }
}

enum _SeatStatus { mine, reserved, available }

class _SeatBox extends StatelessWidget {
  const _SeatBox({
    required this.status,
    required this.size,
    required this.availableBorderColor,
    required this.semanticsLabel,
    this.onTap,
  });

  final _SeatStatus status;
  final double size;
  final Color availableBorderColor;
  final String semanticsLabel;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    // Frame 907:1595..: plain seat squares (no number). Mine is a gold fill +
    // beige border; reserved is the darker navy (#01132d) fill, no border;
    // available has NO fill (the navyDeep card shows through) + the
    // configured border (beige read-only / gold tappable cue).
    final Color fill;
    Border? border;
    switch (status) {
      case _SeatStatus.mine:
        fill = SimfTokens.accent;
        border = Border.all(color: SimfTokens.beigeBorder);
      case _SeatStatus.reserved:
        fill = SimfTokens.navy;
      case _SeatStatus.available:
        fill = SimfTokens.transparent;
        border = Border.all(color: availableBorderColor);
    }
    final box = Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: fill,
        border: border,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSeat),
      ),
    );
    // Only available seats are tappable; reserved / own seats are inert.
    if (status != _SeatStatus.available || onTap == null) {
      return box;
    }
    return Semantics(
      button: true,
      label: semanticsLabel,
      child: GestureDetector(
        onTap: onTap,
        behavior: HitTestBehavior.opaque,
        child: box,
      ),
    );
  }
}

/// The legend row (frame 907:1591): محجوز (deep-navy fill) · متاح (bordered) ·
/// مقعدك (gold fill) — each a label next to its colour swatch. Reads
/// left-to-right like the frame (forced LTR so it never mirrors with the RTL
/// page).
class _Legend extends StatelessWidget {
  const _Legend({
    required this.l10n,
    required this.availableBorderColor,
    required this.swatchSize,
  });

  final AppL10n l10n;
  final Color availableBorderColor;
  final double swatchSize;

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.ltr,
      child: Wrap(
        alignment: WrapAlignment.center,
        spacing: SimfTokens.space2,
        runSpacing: SimfTokens.space2,
        children: <Widget>[
          _LegendItem(
            label: l10n.legendReserved,
            color: SimfTokens.navy,
            size: swatchSize,
          ),
          _LegendItem(
            label: l10n.legendAvailable,
            color: SimfTokens.transparent,
            borderColor: availableBorderColor,
            size: SimfTokens.seatSwatchLg,
          ),
          _LegendItem(
            label: l10n.legendMine,
            color: SimfTokens.accent,
            size: swatchSize,
          ),
        ],
      ),
    );
  }
}

class _LegendItem extends StatelessWidget {
  const _LegendItem({
    required this.color,
    required this.label,
    required this.size,
    this.borderColor,
  });

  final Color color;
  final String label;
  final double size;
  final Color? borderColor;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Text(
          label,
          style: SimfTokens.labelBeigeSm,
        ),
        const SizedBox(width: SimfTokens.space2),
        Container(
          width: size,
          height: size,
          decoration: BoxDecoration(
            color: color,
            border: borderColor != null ? Border.all(color: borderColor!) : null,
            borderRadius: BorderRadius.circular(SimfTokens.radiusSeat),
          ),
        ),
      ],
    );
  }
}
