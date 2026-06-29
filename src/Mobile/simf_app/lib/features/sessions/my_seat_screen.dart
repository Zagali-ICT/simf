import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../app/widgets/simf_svg_icon.dart';
import 'data/seat_map_models.dart';
import 'data/seat_map_repository.dart';

/// Page 018 — مقعدي · My Seat map (#18,
/// `/sessions/:sessionId/my-seat`, **auth-gated**, approved Visitor only),
/// rebuilt to the KSA-Project frame **898:2873 "Your seat"** on the shared
/// navy shell.
///
/// Behaviour is unchanged from the previous build: one read
/// (`GET /app/sessions/{id}/seats`) draws the whole hall grid, every seat
/// coloured by **derived** status — mine / reserved / available (Page_018
/// L-2) — with the caller's own seat highlighted gold. Read-only as drawn.
/// Frame mapping: the circled-back shell header, a navy "الجلسة" card holding
/// the seat (مقعد) + row (الصف) chips, the navy hall card with the gold stage
/// band, the A–H seat grid and the محجوز/متاح/مقعدك legend, then the two gold
/// action buttons (share location / guide me to my seat). Navigate opens the
/// venue map (15); share opens the native sheet (E3).
class MySeatScreen extends ConsumerStatefulWidget {
  const MySeatScreen({required this.sessionId, super.key});

  final String sessionId;

  @override
  ConsumerState<MySeatScreen> createState() => _MySeatScreenState();
}

class _MySeatScreenState extends ConsumerState<MySeatScreen> {
  bool _loading = true;
  bool _error = false;
  bool _notFound = false;
  SessionSeatMap? _map;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
      _notFound = false;
    });
    try {
      final map = await ref
          .read(seatMapRepositoryProvider)
          .getSeatMap(widget.sessionId);
      if (!mounted) {
        return;
      }
      setState(() {
        _map = map;
        _loading = false;
      });
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() {
        _loading = false;
        _notFound = failure.httpStatus == 404;
        _error = failure.httpStatus != 404;
      });
    }
  }

  Future<void> _share(AppL10n l10n, SeatCell cell) async {
    await ref
        .read(seatShareProvider)
        .shareText(l10n.seatShareText(cell.rowLabel, cell.seatNumber));
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.mySeatTitle,
      onBack: () => backOrHome(context),
      tab: SimfTab.sessions,
      body: _buildBody(l10n),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(
        child: CircularProgressIndicator(color: SimfTokens.accent),
      );
    }
    if (_notFound) {
      return SimfEmptyState(
        icon: Icons.event_busy_outlined,
        message: l10n.sessionNotFound,
      );
    }
    if (_error || _map == null) {
      return SimfErrorState(
        message: l10n.seatMapError,
        retryLabel: l10n.retryLabel,
        onRetry: () => unawaited(_load()),
      );
    }
    final map = _map!;
    if (!map.hasLayout) {
      return SimfEmptyState(
        icon: Icons.event_seat_outlined,
        message: l10n.seatMapUnavailable,
      );
    }
    return _SeatMapView(
      map: map,
      l10n: l10n,
      onNavigate: () => context.pushNamed(RouteNames.venueMap),
      onShare: map.myCell == null
          ? null
          : () => unawaited(_share(l10n, map.myCell!)),
    );
  }
}

class _SeatMapView extends StatelessWidget {
  const _SeatMapView({
    required this.map,
    required this.l10n,
    required this.onNavigate,
    this.onShare,
  });

  final SessionSeatMap map;
  final AppL10n l10n;
  final VoidCallback onNavigate;
  final VoidCallback? onShare;

  @override
  Widget build(BuildContext context) {
    // Arabic is primary; the whole page reads right-to-left (frame 898:2873).
    return Directionality(
      textDirection: TextDirection.rtl,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space2,
          SimfTokens.space4,
          SimfTokens.space5,
        ),
        children: <Widget>[
          _SessionCard(map: map, l10n: l10n),
          // Frame gaps: session card (ends y265) → hall card (y289) = 24; hall
          // card (ends y699) → actions (y739) = 40 (no 40 in the spacing scale).
          const SizedBox(height: SimfTokens.space6),
          _HallCard(map: map, l10n: l10n),
          const SizedBox(height: 40),
          _Actions(l10n: l10n, onNavigate: onNavigate, onShare: onShare),
        ],
      ),
    );
  }
}

/// The "الجلسة" card (frame 905:1556): the session label, its title, then the
/// seat (مقعد) + row (الصف) chips — right-aligned on the navy `navyDeep` fill.
class _SessionCard extends StatelessWidget {
  const _SessionCard({required this.map, required this.l10n});

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
            textAlign: TextAlign.right,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textLg,
              fontWeight: FontWeight.w600,
            ),
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
            style: const TextStyle(
              color: SimfTokens.surface,
              fontSize: SimfTokens.textTitle,
              fontWeight: FontWeight.w600,
            ),
          ),
          const SizedBox(height: SimfTokens.space4),
          Row(
            children: <Widget>[
              // RTL: the row (الصف) chip sits at the inline-start (right), the
              // seat (مقعد) chip at the inline-end (left) — frame 905:1576.
              Expanded(
                child: _SeatChip(
                  goldLabel: l10n.rowChipLabel,
                  value: cell != null ? cell.rowLabel : '—',
                  borderColor: SimfTokens.accent,
                  borderWidth: SimfTokens.hairlineBold,
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: _SeatChip(
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

/// One bordered seat/row chip (frame 905:1577 / 905:1579): a gold label word
/// next to its value, centred on a navyDeep fill with a thin gold/beige border.
class _SeatChip extends StatelessWidget {
  const _SeatChip({
    required this.goldLabel,
    required this.value,
    required this.borderColor,
    required this.borderWidth,
  });

  final String goldLabel;
  final String value;
  final Color borderColor;
  final double borderWidth;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 34,
      alignment: Alignment.center,
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        border: Border.all(color: borderColor, width: borderWidth),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text.rich(
        TextSpan(
          children: <InlineSpan>[
            TextSpan(
              text: goldLabel,
              style: const TextStyle(
                color: SimfTokens.accent,
                fontSize: SimfTokens.textSm,
                fontWeight: FontWeight.w600,
              ),
            ),
            const TextSpan(text: ' '),
            TextSpan(
              text: value,
              // Frame 905:1577/1579 — the value (12 / B) is white; only the
              // leading label word (مقعد / الصف) is gold.
              style: const TextStyle(
                color: SimfTokens.surface,
                fontSize: SimfTokens.textMd,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
        textAlign: TextAlign.center,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
    );
  }
}

/// The hall card (frame 907:1708): the gold-bordered stage band, the A–H seat
/// grid, and the محجوز / متاح / مقعدك legend — on the navyDeep fill.
class _HallCard extends StatelessWidget {
  const _HallCard({required this.map, required this.l10n});

  final SessionSeatMap map;
  final AppL10n l10n;

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
          // square and sized to the width (see _SeatRow), centred by this Column,
          // so the full row is always visible — no horizontal scroll.
          Directionality(
            textDirection: TextDirection.ltr,
            child: Column(
              children: <Widget>[
                for (final (index, row) in map.rowLabels.indexed) ...<Widget>[
                  if (index > 0) const SizedBox(height: SimfTokens.space4),
                  _SeatRow(
                    rowLabel: row,
                    seatsPerRow: map.seatsPerRow,
                    reserved: reserved,
                    map: map,
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(height: SimfTokens.space6),
          _Legend(l10n: l10n),
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
      height: 48,
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
        style: const TextStyle(
          fontSize: SimfTokens.textMd,
          color: SimfTokens.accent,
        ),
      ),
    );
  }
}

class _SeatRow extends StatelessWidget {
  const _SeatRow({
    required this.rowLabel,
    required this.seatsPerRow,
    required this.reserved,
    required this.map,
  });

  final String rowLabel;
  final int seatsPerRow;
  final Set<String> reserved;
  final SessionSeatMap map;

  @override
  Widget build(BuildContext context) {
    // `hasLayout` (the caller) already rejects an empty hall; localise that
    // invariant here so the seat-size math below can never divide by zero.
    assert(seatsPerRow > 0, 'seatsPerRow must be > 0 (guarded by hasLayout)');
    // Seats are SQUARES (frame 902:1406 ~20×20). They shrink to fit a narrow
    // (phone) width but never stretch into wide rectangles on a tablet: each is
    // capped at the frame seat size and the whole row is sized to content +
    // centred by the grid Column. Frame 902:1402 = row letter (12px box) + 8px.
    return LayoutBuilder(
      builder: (context, constraints) {
        const labelWidth = 12.0;
        const seatGap = 6.0;
        const maxSeat = 20.0;
        final seatsArea = constraints.maxWidth - labelWidth - SimfTokens.space2;
        final fit = (seatsArea - seatGap * (seatsPerRow - 1)) / seatsPerRow;
        final seat = fit.clamp(0.0, maxSeat).toDouble();
        return Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            SizedBox(
              width: labelWidth,
              child: Text(
                rowLabel,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontSize: SimfTokens.textSm,
                  color: SimfTokens.beigeBorder,
                ),
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
  const _SeatBox({required this.status, required this.size});

  final _SeatStatus status;
  final double size;

  @override
  Widget build(BuildContext context) {
    // Frame 907:1595..: plain seat squares (no number). Mine is a gold fill +
    // beige border; reserved is the darker navy (#01132d) fill, no border;
    // available has NO fill (the navyDeep card shows through) + a beige border.
    final Color fill;
    Border? border;
    switch (status) {
      case _SeatStatus.mine:
        fill = SimfTokens.accent;
        border = Border.all(color: SimfTokens.beigeBorder);
      case _SeatStatus.reserved:
        fill = SimfTokens.navy;
      case _SeatStatus.available:
        fill = Colors.transparent;
        border = Border.all(color: SimfTokens.beigeBorder);
    }
    // A square seat (≤20px, frame 902:1406); [size] is computed per row width.
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: fill,
        border: border,
        borderRadius: BorderRadius.circular(3),
      ),
    );
  }
}

/// The legend row (frame 907:1591): محجوز (deep-navy fill) · متاح (beige
/// border) · مقعدك (gold fill) — each a label next to its colour swatch.
class _Legend extends StatelessWidget {
  const _Legend({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    // Frame 907:1591 — the legend reads left-to-right (محجوز · متاح · مقعدك),
    // each a label then its swatch; force LTR so it matches the frame instead of
    // mirroring with the RTL page.
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
          ),
          _LegendItem(
            label: l10n.legendAvailable,
            color: Colors.transparent,
            border: true,
            size: 16,
          ),
          _LegendItem(
            label: l10n.legendMine,
            color: SimfTokens.accent,
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
    this.border = false,
    this.size = 14,
  });

  final Color color;
  final String label;
  final bool border;
  final double size;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Text(
          label,
          style: const TextStyle(
            color: SimfTokens.beigeBorder,
            fontSize: SimfTokens.textSm,
          ),
        ),
        const SizedBox(width: SimfTokens.space2),
        Container(
          width: size,
          height: size,
          decoration: BoxDecoration(
            color: color,
            border: border ? Border.all(color: SimfTokens.beigeBorder) : null,
            borderRadius: BorderRadius.circular(3),
          ),
        ),
      ],
    );
  }
}

/// The action row (frame 908:1733 / 908:1737): a gold-outlined "share location"
/// next to a gold-filled "guide me to my seat".
class _Actions extends StatelessWidget {
  const _Actions({required this.l10n, required this.onNavigate, this.onShare});

  final AppL10n l10n;
  final VoidCallback onNavigate;
  final VoidCallback? onShare;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        // RTL: the gold-filled "guide me" CTA sits at the inline-start (right),
        // the outlined "share location" at the inline-end (left) — frame
        // 908:1733 / 908:1737.
        Expanded(
          child: FilledButton.icon(
            onPressed: onNavigate,
            style: FilledButton.styleFrom(
              backgroundColor: SimfTokens.accent,
              foregroundColor: SimfTokens.surface,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              ),
              padding: const EdgeInsets.symmetric(
                horizontal: SimfTokens.space4,
                vertical: SimfTokens.space3,
              ),
            ),
            icon: const SimfSvgIcon(
              'assets/icons/ic_location.svg',
              size: 18,
              color: SimfTokens.surface,
            ),
            label: Text(
              l10n.navigateToSeat,
              style: const TextStyle(
                fontSize: SimfTokens.textSm,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: OutlinedButton.icon(
            onPressed: onShare,
            style: OutlinedButton.styleFrom(
              foregroundColor: SimfTokens.surface,
              side: const BorderSide(color: SimfTokens.accent),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              ),
              padding: const EdgeInsets.symmetric(
                horizontal: SimfTokens.space4,
                vertical: SimfTokens.space3,
              ),
            ),
            icon: const Icon(Icons.share_outlined, size: 18),
            label: Text(
              l10n.shareLocation,
              style: const TextStyle(
                fontSize: SimfTokens.textSm,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ),
      ],
    );
  }
}
