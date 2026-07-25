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
///   (and the currently **selected**) seats become tappable (reserved / own
///   seats stay inert) and carry a `rowLabel + seatNumber` Semantics button.
/// * [selectedRowLabel] / [selectedSeatNumber] — the seat the picker has tapped
///   but not yet confirmed; drawn gold with its navy number (null = none).
/// * [busy] — freezes the taps while a reserve call is in flight.
/// * [maxSeatSize] — seats are squares that shrink to fit the width but never
///   grow past this cap (frame 902:1406 ≈ 20; the picker renders 26).
/// * [availableBorderColor] — beige on My-Seat (the frame), gold on the picker
///   as the tappable cue; the legend's available swatch follows it.
/// * [swatchSize] — the reserved/mine legend swatches (14 on My-Seat per the
///   frame, 16 on the picker); the available swatch is always 16.
///
/// Each row is drawn at its own width: seats are sized to the widest row
/// ([SessionSeatMap.maxSeatsPerRow]) so every square is identical, and a short
/// row draws fewer seats centred under the stage with its label pinned start.
class HallSeatMapCard extends StatelessWidget {
  const HallSeatMapCard({
    required this.map,
    required this.l10n,
    this.onSeatTap,
    this.selectedRowLabel,
    this.selectedSeatNumber,
    this.busy = false,
    this.maxSeatSize = SimfTokens.seatCapDefault,
    this.availableBorderColor = SimfTokens.beigeBorder,
    this.swatchSize = SimfTokens.seatSwatchSm,
    super.key,
  });

  final SessionSeatMap map;
  final AppL10n l10n;
  final void Function(String rowLabel, int seatNumber)? onSeatTap;
  final String? selectedRowLabel;
  final int? selectedSeatNumber;
  final bool busy;
  final double maxSeatSize;
  final Color availableBorderColor;
  final double swatchSize;

  @override
  Widget build(BuildContext context) {
    final reserved = map.reservedKeys();
    // Seats are sized to the WIDEST row so every square is identical; a short
    // row simply draws fewer of them (centred by _SeatGridRow).
    final columns = map.maxSeatsPerRow;
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
                    columns: columns,
                    seatCount: map.seatsInRow(index),
                    reserved: reserved,
                    map: map,
                    l10n: l10n,
                    maxSeatSize: maxSeatSize,
                    availableBorderColor: availableBorderColor,
                    selectedRowLabel: selectedRowLabel,
                    selectedSeatNumber: selectedSeatNumber,
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
    required this.columns,
    required this.seatCount,
    required this.reserved,
    required this.map,
    required this.l10n,
    required this.maxSeatSize,
    required this.availableBorderColor,
    this.selectedRowLabel,
    this.selectedSeatNumber,
    this.onSeatTap,
  });

  final String rowLabel;
  // The widest row's count — the seat SIZE denominator so all rows match.
  final int columns;
  // This row's own count — how many seats it actually draws.
  final int seatCount;
  final Set<String> reserved;
  final SessionSeatMap map;
  final AppL10n l10n;
  final double maxSeatSize;
  final Color availableBorderColor;
  final String? selectedRowLabel;
  final int? selectedSeatNumber;
  final void Function(String rowLabel, int seatNumber)? onSeatTap;

  _SeatStatus _statusFor(int seat) {
    if (map.isMine(rowLabel, seat)) {
      return _SeatStatus.mine;
    }
    if (reserved.contains('$rowLabel:$seat')) {
      return _SeatStatus.reserved;
    }
    if (selectedRowLabel == rowLabel && selectedSeatNumber == seat) {
      return _SeatStatus.selected;
    }
    return _SeatStatus.available;
  }

  // Reuses the legend words (محجوز / مقعدك) so a screen reader announces the
  // seat state without relying on colour; available/selected keep the bare id.
  String _semanticsFor(_SeatStatus status, int seat) {
    final id = '$rowLabel$seat';
    switch (status) {
      case _SeatStatus.reserved:
        return '${l10n.legendReserved} $id';
      case _SeatStatus.mine:
        return '${l10n.legendMine} $id';
      case _SeatStatus.selected:
      case _SeatStatus.available:
        return id;
    }
  }

  Widget _seat(int seat, double size) {
    final status = _statusFor(seat);
    final tappable = onSeatTap != null &&
        (status == _SeatStatus.available || status == _SeatStatus.selected);
    return _SeatBox(
      size: size,
      seatNumber: seat,
      status: status,
      availableBorderColor: availableBorderColor,
      semanticsLabel: _semanticsFor(status, seat),
      onTap: tappable ? () => onSeatTap!(rowLabel, seat) : null,
    );
  }

  @override
  Widget build(BuildContext context) {
    // `hasLayout` (the caller) already rejects an empty hall; localise that
    // invariant here so the seat-size math below can never divide by zero.
    assert(columns > 0, 'columns must be > 0 (guarded by hasLayout)');
    // Seats are SQUARES (frame 902:1406). They shrink to fit a narrow (phone)
    // width but never stretch into wide rectangles on a tablet: each is capped
    // at [maxSeatSize] and sized to [columns] (the widest row) so every row's
    // squares match. Frame 902:1402 = row letter (12px box) + 8px.
    return LayoutBuilder(
      builder: (context, constraints) {
        const labelWidth = SimfTokens.seatRowLabelWidth;
        const seatGap = SimfTokens.gap6;
        final seatsArea = constraints.maxWidth - labelWidth - SimfTokens.space2;
        final fit = (seatsArea - seatGap * (columns - 1)) / columns;
        final seat = fit.clamp(0.0, maxSeatSize);
        return Row(
          children: <Widget>[
            // The row letter is pinned at the start so short rows stay aligned.
            SizedBox(
              width: labelWidth,
              child: Text(
                rowLabel,
                textAlign: TextAlign.center,
                style: SimfTokens.labelBeigeSm,
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            // Short rows draw fewer seats, centred in the shared seats area.
            Expanded(
              child: Center(
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    for (var s = 1; s <= seatCount; s++) ...<Widget>[
                      if (s > 1) const SizedBox(width: seatGap),
                      _seat(s, seat),
                    ],
                  ],
                ),
              ),
            ),
          ],
        );
      },
    );
  }
}

enum _SeatStatus { mine, selected, reserved, available }

class _SeatBox extends StatelessWidget {
  const _SeatBox({
    required this.status,
    required this.size,
    required this.seatNumber,
    required this.availableBorderColor,
    required this.semanticsLabel,
    this.onTap,
  });

  final _SeatStatus status;
  final double size;
  final int seatNumber;
  final Color availableBorderColor;
  final String semanticsLabel;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    // Frame 907:1595..: each square carries its centred seat number; a reserved
    // or your-seat square swaps the number for a state icon (a colour-blind cue
    // that also drives Semantics). Mine is a gold fill + beige border; selected
    // is the same gold with its navy number; reserved is the darker navy
    // (#01132d) fill; available has NO fill (the navyDeep card shows through) +
    // the configured border (beige read-only / gold tappable cue).
    final Color fill;
    Border? border;
    Widget glyph;
    switch (status) {
      case _SeatStatus.mine:
        fill = SimfTokens.accent;
        border = Border.all(color: SimfTokens.beigeBorder);
        glyph = const Icon(
          Icons.check,
          size: SimfTokens.seatStateIconSize,
          color: SimfTokens.navy,
        );
      case _SeatStatus.selected:
        fill = SimfTokens.accent;
        border = Border.all(color: SimfTokens.beigeBorder);
        glyph = Text('$seatNumber', style: SimfTokens.seatNumberOnGold);
      case _SeatStatus.reserved:
        fill = SimfTokens.navy;
        glyph = const Icon(
          Icons.close,
          size: SimfTokens.seatStateIconSize,
          color: SimfTokens.beigeBorder,
        );
      case _SeatStatus.available:
        fill = SimfTokens.transparent;
        border = Border.all(color: availableBorderColor);
        glyph = Text('$seatNumber', style: SimfTokens.seatNumberOnDark);
    }
    final box = Container(
      width: size,
      height: size,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: fill,
        border: border,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSeat),
      ),
      // The numeral / icon is decorative — the cell's own Semantics label below
      // carries the seat id + state, so keep the glyph out of the a11y tree.
      child: ExcludeSemantics(child: glyph),
    );
    // Available / selected seats are tappable buttons; reserved / own seats are
    // inert but still carry a Semantics label for the screen reader.
    final selectable =
        status == _SeatStatus.available || status == _SeatStatus.selected;
    if (selectable && onTap != null) {
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
    if (status == _SeatStatus.reserved || status == _SeatStatus.mine) {
      return Semantics(label: semanticsLabel, child: box);
    }
    return box;
  }
}

/// The legend row (frame 907:1591): محجوز (deep-navy fill) · متاح (bordered) ·
/// مقعدك (gold fill) — each a label next to its colour swatch. The reserved and
/// mine swatches mirror the in-grid state icons. Reads left-to-right like the
/// frame (forced LTR so it never mirrors with the RTL page).
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
            icon: Icons.close,
            iconColor: SimfTokens.beigeBorder,
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
            icon: Icons.check,
            iconColor: SimfTokens.navy,
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
    this.icon,
    this.iconColor,
  });

  final Color color;
  final String label;
  final double size;
  final Color? borderColor;
  final IconData? icon;
  final Color? iconColor;

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
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: color,
            border: borderColor != null ? Border.all(color: borderColor!) : null,
            borderRadius: BorderRadius.circular(SimfTokens.radiusSeat),
          ),
          child: icon != null
              ? Icon(
                  icon,
                  size: SimfTokens.seatStateIconSize,
                  color: iconColor,
                )
              : null,
        ),
      ],
    );
  }
}
