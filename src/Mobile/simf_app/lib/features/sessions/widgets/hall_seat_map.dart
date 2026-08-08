import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/seat_map_models.dart';
import 'stage_bar.dart';
import 'legend.dart';
import 'tier_legend.dart';

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
/// A12 — a held seat draws in one of two states, matching the Control
/// Panel's live-hall map: **محجوز / reserved** while the holder has not
/// arrived, and **confirmed** (green, `how_to_reg`) once they scanned in at
/// the gate. The confirmed legend appears only when one exists.
///
/// Each row is drawn at its own width at a FIXED, readable seat size (never
/// shrunk to fit): every square is identical and a short row draws fewer seats
/// with its label pinned start. A hall wider than the card SCROLLS horizontally
/// (drag left/right) so all seats stay legible; a hall that already fits is
/// centred and does not scroll.
class HallSeatMapCard extends StatefulWidget {
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
    this.inspectMode = false,
    super.key,
  });

  final SessionSeatMap map;
  final AppL10n l10n;
  final void Function(String rowLabel, int seatNumber)? onSeatTap;
  final String? selectedRowLabel;
  final int? selectedSeatNumber;
  final bool busy;
  // The fixed square size each seat is drawn at (my-seat 20 / picker 26). Seats
  // are never shrunk below this to fit — a wide row scrolls instead.
  final double maxSeatSize;
  final Color availableBorderColor;
  final double swatchSize;

  /// D-771 — INSPECT mode, for the staff seating desk. False (default) keeps the
  /// visitor picker's booking semantics: only an available (or already selected)
  /// seat is tappable, and a seat whose tier the caller may not book draws locked
  /// and inert. True makes EVERY seat tappable — reserved, own, VVIP, VIP alike —
  /// because the desk is looking up occupants, not reserving, so neither the
  /// reservation state nor tier eligibility should block a tap.
  final bool inspectMode;

  @override
  State<HallSeatMapCard> createState() => _HallSeatMapCardState();
}

class _HallSeatMapCardState extends State<HallSeatMapCard> {
  // Owns the grid's horizontal scroll so a wide hall can be dragged left/right
  // and the scrollbars hint there are more seats off the card edges. Two
  // controllers so the grid pans on BOTH axes inside a bounded viewport:
  // _hScroll (left/right seats), _vScroll (up/down rows).
  final ScrollController _hScroll = ScrollController();
  final ScrollController _vScroll = ScrollController();

  @override
  void dispose() {
    _hScroll.dispose();
    _vScroll.dispose();
    super.dispose();
  }

  // The row-letter column widens to fit the longest label so multi-char rows
  // (VVIP / VIP01 / A001) render on one line instead of wrapping; a single-
  // letter hall keeps the Figma 12px column.
  double _rowLabelWidth(List<String> rows) {
    var longest = 1;
    for (final label in rows) {
      if (label.length > longest) {
        longest = label.length;
      }
    }
    return longest <= 1
        ? SimfTokens.seatRowLabelWidth
        : longest * SimfTokens.seatRowLabelCharWidth;
  }

  @override
  Widget build(BuildContext context) {
    final map = widget.map;
    final l10n = widget.l10n;
    // A12 — the grid needs the CELL, not just "is this seat taken": a held
    // seat whose holder has checked in draws confirmed, not reserved.
    final reserved = map.reservedByKey();
    final rowLabelWidth = _rowLabelWidth(map.rowLabels);
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        children: <Widget>[
          StageBar(label: l10n.stageLabelBilingual),
          const SizedBox(height: SimfTokens.space6),
          // The hall plan keeps the stage at the top and seat columns in venue
          // order — do not mirror the grid geometry in RTL (L-7), so it is
          // forced LTR. Seats are a FIXED size (see _SeatGridRow) inside a
          // bounded viewport that pans on BOTH axes: the grid scrolls left/right
          // when wider than the card and up/down when taller than the viewport,
          // each with its own scrollbar (scoped by axis). A hall that fits
          // shows fully — centred horizontally — and does not scroll.
          Directionality(
            textDirection: TextDirection.ltr,
            child: LayoutBuilder(
              builder: (context, constraints) {
                final grid = Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    for (final (index, row)
                        in map.rowLabels.indexed) ...<Widget>[
                      if (index > 0)
                        const SizedBox(height: SimfTokens.space4),
                      _SeatGridRow(
                        rowLabel: row,
                        seatCount: map.seatsInRow(index),
                        // D-771 — the row's tier + whether THIS caller may book it;
                        // an ineligible row draws its seats locked and inert.
                        tier: map.tierOfRow(index),
                        eligible:
                            widget.inspectMode || map.canReserveRow(index),
                        inspectMode: widget.inspectMode,
                        seatSize: widget.maxSeatSize,
                        rowLabelWidth: rowLabelWidth,
                        reserved: reserved,
                        map: map,
                        l10n: l10n,
                        availableBorderColor: widget.availableBorderColor,
                        selectedRowLabel: widget.selectedRowLabel,
                        selectedSeatNumber: widget.selectedSeatNumber,
                        onSeatTap: widget.busy ? null : widget.onSeatTap,
                      ),
                    ],
                    // Clears the horizontal scrollbar track from the last row.
                    const SizedBox(height: SimfTokens.space3),
                  ],
                );
                // Cap the seat area's height so a tall hall scrolls vertically
                // instead of pushing the legend / CTAs off the page; a short
                // hall keeps its natural height (no empty band).
                return ConstrainedBox(
                  constraints: const BoxConstraints(
                    maxHeight: SimfTokens.seatViewportMaxHeight,
                  ),
                  child: Scrollbar(
                    controller: _vScroll,
                    thumbVisibility: true,
                    notificationPredicate: (notif) =>
                        notif.metrics.axis == Axis.vertical,
                    child: SingleChildScrollView(
                      controller: _vScroll,
                      child: Scrollbar(
                        controller: _hScroll,
                        thumbVisibility: true,
                        notificationPredicate: (notif) =>
                            notif.metrics.axis == Axis.horizontal,
                        child: SingleChildScrollView(
                          controller: _hScroll,
                          scrollDirection: Axis.horizontal,
                          child: ConstrainedBox(
                            // A narrow hall centres under the stage; a wider
                            // hall exceeds this and scrolls left/right.
                            constraints: BoxConstraints(
                              minWidth: constraints.maxWidth,
                            ),
                            child: Center(child: grid),
                          ),
                        ),
                      ),
                    ),
                  ),
                );
              },
            ),
          ),
          const SizedBox(height: SimfTokens.space6),
          Legend(
            l10n: l10n,
            availableBorderColor: widget.availableBorderColor,
            swatchSize: widget.swatchSize,
            // A12 — the confirmed swatch appears only once someone has
            // actually checked in, so a hall with nobody through the gate
            // keeps the shipped three-item legend.
            showConfirmed: map.hasConfirmed,
          ),
          // D-771 — the tier legend only appears for a hall that actually has
          // tiered rows, so a plain hall keeps the shipped three-item legend.
          if (map.hasTiers) ...<Widget>[
            const SizedBox(height: SimfTokens.space3),
            TierLegend(l10n: l10n, swatchSize: widget.swatchSize),
          ],
        ],
      ),
    );
  }
}

class _SeatGridRow extends StatelessWidget {
  const _SeatGridRow({
    required this.rowLabel,
    required this.seatCount,
    required this.tier,
    required this.eligible,
    required this.inspectMode,
    required this.seatSize,
    required this.rowLabelWidth,
    required this.reserved,
    required this.map,
    required this.l10n,
    required this.availableBorderColor,
    this.selectedRowLabel,
    this.selectedSeatNumber,
    this.onSeatTap,
  });

  final String rowLabel;
  // This row's own count — how many seats it actually draws.
  final int seatCount;
  // D-771 — the row's seat tier, drawn as a start-edge band + a row-label caption.
  final SeatTier tier;
  // D-771 — false when THIS caller may not book this row (a VVIP protocol row, or
  // a VIP row for a non-VIP visitor): its free seats draw locked and inert.
  final bool eligible;
  // D-771 — the staff seating desk: every seat is tappable (occupant lookup), so
  // a reserved / own seat is not inert here.
  final bool inspectMode;
  // The fixed square size every seat is drawn at (no shrink-to-fit); all rows
  // share it so seats align column-for-column and a wide row scrolls off-edge.
  final double seatSize;
  // The shared row-letter column width (sized to the longest label so all rows
  // align and multi-char labels do not wrap).
  final double rowLabelWidth;
  // A12 — occupied seats by `row:seat` key. The CELL is needed (not a bare
  // key set) so a checked-in holder draws the confirmed state.
  final Map<String, SeatCell> reserved;
  final SessionSeatMap map;
  final AppL10n l10n;
  final Color availableBorderColor;
  final String? selectedRowLabel;
  final int? selectedSeatNumber;
  final void Function(String rowLabel, int seatNumber)? onSeatTap;

  _SeatStatus _statusFor(int seat) {
    if (map.isMine(rowLabel, seat)) {
      return _SeatStatus.mine;
    }
    final held = reserved['$rowLabel:$seat'];
    if (held != null) {
      // A12 — the fourth state: a holder who scanned in at the hall gate is
      // CONFIRMED, not merely reserved. The CP live-hall map has always
      // drawn this; the app could not, because the wire key was dropped on
      // decode.
      return held.checkedIn ? _SeatStatus.confirmed : _SeatStatus.reserved;
    }
    if (selectedRowLabel == rowLabel && selectedSeatNumber == seat) {
      return _SeatStatus.selected;
    }
    // D-771 — a free seat the caller may not book is INELIGIBLE, not available:
    // it draws locked (and never tappable) with a spoken explanation.
    if (!eligible) {
      return _SeatStatus.ineligible;
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
      case _SeatStatus.confirmed:
        return '${l10n.legendConfirmed} $id';
      case _SeatStatus.mine:
        return '${l10n.legendMine} $id';
      case _SeatStatus.ineligible:
        // D-771 — say WHY, not just "unavailable": a VVIP row is protocol
        // seating, a VIP row needs the VIP tier.
        return '$id · ${tier.isVvip ? l10n.seatTierVvipLocked : l10n.seatTierVipLocked}';
      case _SeatStatus.selected:
      case _SeatStatus.available:
        return id;
    }
  }

  Widget _seat(int seat) {
    final status = _statusFor(seat);
    // D-771 — in inspect mode EVERY seat is tappable (the desk looks up whoever
    // is there); otherwise only an available / already-selected seat is.
    final tappable = onSeatTap != null &&
        (inspectMode ||
            status == _SeatStatus.available ||
            status == _SeatStatus.selected);
    return _SeatBox(
      size: seatSize,
      seatNumber: seat,
      status: status,
      availableBorderColor: availableBorderColor,
      semanticsLabel: _semanticsFor(status, seat),
      onTap: tappable ? () => onSeatTap!(rowLabel, seat) : null,
    );
  }

  @override
  Widget build(BuildContext context) {
    // Seats are SQUARES (frame 902:1406) drawn at a fixed [seatSize] — never
    // shrunk to fit — so every seat and its reserved / available state stays
    // legible; a row wider than the card scrolls (see HallSeatMapCard). Frame
    // 902:1402 = row letter (12px box) + 8px, pinned start so rows align.
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        // D-771 — the row's tier band: a thin coloured bar at the row's start edge
        // (the grid is force-LTR, so "start" is always the left of the plan).
        Container(
          width: SimfTokens.hairlineBold,
          height: seatSize,
          color: _tierBandColor(tier),
        ),
        const SizedBox(width: SimfTokens.space2),
        SizedBox(
          width: rowLabelWidth,
          child: Text(
            rowLabel,
            textAlign: TextAlign.center,
            style: SimfTokens.labelBeigeSm,
          ),
        ),
        const SizedBox(width: SimfTokens.space2),
        for (var s = 1; s <= seatCount; s++) ...<Widget>[
          if (s > 1) const SizedBox(width: SimfTokens.gap6),
          _seat(s),
        ],
      ],
    );
  }
}

/// D-771 — the tier band colour. Deep red = VVIP protocol, deep teal = VIP, the
/// card fill (invisible) = Normal, matching the CP seat plan and the seeded
/// VVIP / VIP badge colours.
Color _tierBandColor(SeatTier tier) => switch (tier) {
      SeatTier.vvip => SimfTokens.seatTierVvip,
      SeatTier.vip => SimfTokens.seatTierVip,
      SeatTier.normal => SimfTokens.transparent,
    };

enum _SeatStatus { mine, selected, reserved, confirmed, available, ineligible }

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
          size: SimfTokens.seatCellIconSize,
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
          size: SimfTokens.seatCellIconSize,
          color: SimfTokens.beigeBorder,
        );
      case _SeatStatus.confirmed:
        // A12 — distinct from a plain reserved square on BOTH channels (green
        // fill AND a different glyph), so the fourth state reads without
        // relying on colour alone.
        fill = SimfTokens.seatConfirmed;
        border = Border.all(color: SimfTokens.seatConfirmed);
        glyph = const Icon(
          Icons.how_to_reg,
          size: SimfTokens.seatCellIconSize,
          color: SimfTokens.surface,
        );
      case _SeatStatus.ineligible:
        // D-771 — a free seat this caller may not book: no fill, a muted border and
        // a padlock, so it never reads as "available" nor as "someone sits here".
        fill = SimfTokens.transparent;
        border = Border.all(color: SimfTokens.beigeBorder);
        glyph = const Icon(
          Icons.lock_outline,
          size: SimfTokens.seatCellIconSize,
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
    // inert but still carry a Semantics label for the screen reader. D-771: the
    // staff desk passes onTap for EVERY seat, so the button branch is keyed off
    // onTap alone — a tappable reserved seat still announces its state.
    if (onTap != null) {
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
    if (status == _SeatStatus.reserved ||
        status == _SeatStatus.confirmed ||
        status == _SeatStatus.mine ||
        status == _SeatStatus.ineligible) {
      return Semantics(label: semanticsLabel, child: box);
    }
    return box;
  }
}

