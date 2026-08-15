import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/widgets/legend.dart';
import 'package:simf_app/features/sessions/widgets/seat_grid_row.dart';
import 'package:simf_app/features/sessions/widgets/stage_bar.dart';
import 'package:simf_app/features/sessions/widgets/tier_legend.dart';

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

  /// D-771 — INSPECT mode, for the staff seating desk. False (default) keeps
  /// the visitor picker's booking semantics: only an available (or already
  /// selected) seat is tappable, and a seat whose tier the caller may not book
  /// draws locked and inert. True makes EVERY seat tappable — reserved, own,
  /// VVIP, VIP alike — because the desk is looking up occupants, not reserving,
  /// so neither the reservation state nor tier eligibility should block a tap.
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
          // forced LTR. Seats are a FIXED size (see SeatGridRow) inside a
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
                      SeatGridRow(
                        rowLabel: row,
                        seatCount: map.seatsInRow(index),
                        // D-771 — the row's tier + whether THIS caller may book
                        // it; an ineligible row draws its seats locked and
                        // inert.
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
