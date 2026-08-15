import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';

/// One rendered row of the hall seat map, its seat squares, and the seat
/// state they share.
///
/// These four move together because they are one idea: SeatStatus is the
/// vocabulary, tierBandColor and the two members of SeatGridRow speak it,
/// and SeatBox renders it. Splitting them would mean making the enum
/// public to no other reader.
class SeatGridRow extends StatelessWidget {
  const SeatGridRow({
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
    super.key,
    this.selectedRowLabel,
    this.selectedSeatNumber,
    this.onSeatTap,
  });

  final String rowLabel;
  // This row's own count — how many seats it actually draws.
  final int seatCount;
  // D-771 — the row's seat tier, drawn as a start-edge band + a row-label
  // caption.
  final SeatTier tier;
  // D-771 — false when THIS caller may not book this row (a VVIP protocol row,
  // or a VIP row for a non-VIP visitor): its free seats draw locked and inert.
  final bool eligible;
  // D-771 — the staff seating desk: every seat is tappable (occupant lookup),
  // so a reserved / own seat is not inert here.
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

  SeatStatus _statusFor(int seat) {
    if (map.isMine(rowLabel, seat)) {
      return SeatStatus.mine;
    }
    final held = reserved['$rowLabel:$seat'];
    if (held != null) {
      // A12 — the fourth state: a holder who scanned in at the hall gate is
      // CONFIRMED, not merely reserved. The CP live-hall map has always
      // drawn this; the app could not, because the wire key was dropped on
      // decode.
      return held.checkedIn ? SeatStatus.confirmed : SeatStatus.reserved;
    }
    if (selectedRowLabel == rowLabel && selectedSeatNumber == seat) {
      return SeatStatus.selected;
    }
    // D-771 — a free seat the caller may not book is INELIGIBLE, not available:
    // it draws locked (and never tappable) with a spoken explanation.
    if (!eligible) {
      return SeatStatus.ineligible;
    }
    return SeatStatus.available;
  }

  // Reuses the legend words (محجوز / مقعدك) so a screen reader announces the
  // seat state without relying on colour; available/selected keep the bare id.
  String _semanticsFor(SeatStatus status, int seat) {
    final id = '$rowLabel$seat';
    switch (status) {
      case SeatStatus.reserved:
        return '${l10n.legendReserved} $id';
      case SeatStatus.confirmed:
        return '${l10n.legendConfirmed} $id';
      case SeatStatus.mine:
        return '${l10n.legendMine} $id';
      case SeatStatus.ineligible:
        // D-771 — say WHY, not just "unavailable": a VVIP row is protocol
        // seating, a VIP row needs the VIP tier.
        final locked =
            tier.isVvip ? l10n.seatTierVvipLocked : l10n.seatTierVipLocked;
        return '$id · $locked';
      case SeatStatus.selected:
      case SeatStatus.available:
        return id;
    }
  }

  Widget _seat(int seat) {
    final status = _statusFor(seat);
    // D-771 — in inspect mode EVERY seat is tappable (the desk looks up whoever
    // is there); otherwise only an available / already-selected seat is.
    final tappable = onSeatTap != null &&
        (inspectMode ||
            status == SeatStatus.available ||
            status == SeatStatus.selected);
    return SeatBox(
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
        // D-771 — the row's tier band: a thin coloured bar at the row's start
        // edge (the grid is force-LTR, so "start" is always the left of the
        // plan).
        Container(
          width: SimfTokens.hairlineBold,
          height: seatSize,
          color: tierBandColor(tier),
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
Color tierBandColor(SeatTier tier) => switch (tier) {
      SeatTier.vvip => SimfTokens.seatTierVvip,
      SeatTier.vip => SimfTokens.seatTierVip,
      SeatTier.normal => SimfTokens.transparent,
    };

enum SeatStatus { mine, selected, reserved, confirmed, available, ineligible }

class SeatBox extends StatelessWidget {
  const SeatBox({
    required this.status,
    required this.size,
    required this.seatNumber,
    required this.availableBorderColor,
    required this.semanticsLabel,
    super.key,
    this.onTap,
  });

  final SeatStatus status;
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
      case SeatStatus.mine:
        fill = SimfTokens.accent;
        border = Border.all(color: SimfTokens.beigeBorder);
        glyph = const Icon(
          Icons.check,
          size: SimfTokens.seatCellIconSize,
          color: SimfTokens.navy,
        );
      case SeatStatus.selected:
        fill = SimfTokens.accent;
        border = Border.all(color: SimfTokens.beigeBorder);
        glyph = Text('$seatNumber', style: SimfTokens.seatNumberOnGold);
      case SeatStatus.reserved:
        fill = SimfTokens.navy;
        glyph = const Icon(
          Icons.close,
          size: SimfTokens.seatCellIconSize,
          color: SimfTokens.beigeBorder,
        );
      case SeatStatus.confirmed:
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
      case SeatStatus.ineligible:
        // D-771 — a free seat this caller may not book: no fill, a muted border
        // and a padlock, so it never reads as "available" nor as "someone sits
        // here".
        fill = SimfTokens.transparent;
        border = Border.all(color: SimfTokens.beigeBorder);
        glyph = const Icon(
          Icons.lock_outline,
          size: SimfTokens.seatCellIconSize,
          color: SimfTokens.beigeBorder,
        );
      case SeatStatus.available:
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
    if (status == SeatStatus.reserved ||
        status == SeatStatus.confirmed ||
        status == SeatStatus.mine ||
        status == SeatStatus.ineligible) {
      return Semantics(label: semanticsLabel, child: box);
    }
    return box;
  }
}
