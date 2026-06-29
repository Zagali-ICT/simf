import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import 'data/seat_map_models.dart';
import 'data/seat_map_repository.dart';

/// D-485 — **Seat picker** (`/sessions/:sessionId/pick-seat`, approved Visitor).
/// An assigned-seat session's selectable hall grid: tap an **available** seat to
/// reserve it, or auto-pick one. The booking is created **Pending** — the Control
/// Panel approves it, and the approved/rejected notification arrives in the
/// inbox. Reuses the shipped seat endpoints (`GET …/seats` to draw,
/// `POST …/seats/reserve` / `…/reserve-random` to hold). On success it pops with
/// `true` so the session page reloads to show the held reservation.
class SeatPickerScreen extends ConsumerStatefulWidget {
  const SeatPickerScreen({required this.sessionId, super.key});

  final String sessionId;

  @override
  ConsumerState<SeatPickerScreen> createState() => _SeatPickerScreenState();
}

class _SeatPickerScreenState extends ConsumerState<SeatPickerScreen> {
  bool _loading = true;
  bool _error = false;
  bool _notFound = false;
  bool _busy = false;
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
      final map =
          await ref.read(seatMapRepositoryProvider).getSeatMap(widget.sessionId);
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

  Future<void> _reserve(AppL10n l10n, String row, int seat) =>
      _hold(l10n, (repo) => repo.reserveSeat(widget.sessionId, rowLabel: row, seatNumber: seat));

  Future<void> _reserveRandom(AppL10n l10n) =>
      _hold(l10n, (repo) => repo.reserveRandom(widget.sessionId));

  /// Shared hold flow: guard against a double-tap, call the endpoint, then —
  /// only if still mounted — toast the outcome and (on success) pop with `true`
  /// so the session page reloads. `_busy` is reset in a `finally` so any throw
  /// (not just ApiFailure) un-freezes the grid; the post-await navigation is
  /// `mounted`-guarded so backing out mid-request can't pop an unrelated route.
  Future<void> _hold(
    AppL10n l10n,
    Future<MyReservation> Function(SeatMapRepository repo) action,
  ) async {
    if (_busy) {
      return;
    }
    setState(() => _busy = true);
    final messenger = ScaffoldMessenger.of(context);
    final navigator = Navigator.of(context);
    var reserved = false;
    try {
      await action(ref.read(seatMapRepositoryProvider));
      reserved = true;
    } on ApiFailure {
      // Reported below once we know the screen is still mounted.
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
    if (!mounted) {
      return;
    }
    if (reserved) {
      messenger.showSnackBar(SnackBar(content: Text(l10n.seatReservedToast)));
      navigator.pop(true);
    } else {
      messenger.showSnackBar(SnackBar(content: Text(l10n.seatReserveFailed)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.seatPickerTitle,
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
          Text(
            map.localizedSessionTitle(l10n.isArabic) ?? l10n.seatPickerTitle,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: SimfTokens.surface,
              fontSize: SimfTokens.textTitle,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            l10n.seatPickerHint,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
            ),
          ),
          const SizedBox(height: SimfTokens.space5),
          _PickerHallCard(
            map: map,
            l10n: l10n,
            busy: _busy,
            onPick: (row, seat) => unawaited(_reserve(l10n, row, seat)),
          ),
          const SizedBox(height: SimfTokens.space5),
          FilledButton.icon(
            onPressed: _busy ? null : () => unawaited(_reserveRandom(l10n)),
            style: FilledButton.styleFrom(
              minimumSize: const Size.fromHeight(48),
              backgroundColor: SimfTokens.accent,
              foregroundColor: SimfTokens.surface,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              ),
            ),
            icon: const Icon(Icons.shuffle, size: 20),
            label: Text(
              l10n.seatPickerRandomCta,
              style: const TextStyle(
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textLg,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// The hall card with the gold stage band, the selectable seat grid and the
/// legend — mirrors the My-Seat hall card, but available seats are tappable.
class _PickerHallCard extends StatelessWidget {
  const _PickerHallCard({
    required this.map,
    required this.l10n,
    required this.busy,
    required this.onPick,
  });

  final SessionSeatMap map;
  final AppL10n l10n;
  final bool busy;
  final void Function(String rowLabel, int seatNumber) onPick;

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
          Container(
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
              l10n.stageLabelBilingual,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: SimfTokens.textMd,
                color: SimfTokens.accent,
              ),
            ),
          ),
          const SizedBox(height: SimfTokens.space6),
          // The hall plan keeps stage-at-top + venue seat order — do not mirror
          // the grid in RTL.
          Directionality(
            textDirection: TextDirection.ltr,
            child: Column(
              children: <Widget>[
                for (final (index, row) in map.rowLabels.indexed) ...<Widget>[
                  if (index > 0) const SizedBox(height: SimfTokens.space4),
                  _PickerRow(
                    rowLabel: row,
                    seatsPerRow: map.seatsPerRow,
                    reserved: reserved,
                    map: map,
                    busy: busy,
                    onPick: onPick,
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(height: SimfTokens.space6),
          _PickerLegend(l10n: l10n),
        ],
      ),
    );
  }
}

class _PickerRow extends StatelessWidget {
  const _PickerRow({
    required this.rowLabel,
    required this.seatsPerRow,
    required this.reserved,
    required this.map,
    required this.busy,
    required this.onPick,
  });

  final String rowLabel;
  final int seatsPerRow;
  final Set<String> reserved;
  final SessionSeatMap map;
  final bool busy;
  final void Function(String rowLabel, int seatNumber) onPick;

  @override
  Widget build(BuildContext context) {
    assert(seatsPerRow > 0, 'seatsPerRow must be > 0 (guarded by hasLayout)');
    return LayoutBuilder(
      builder: (context, constraints) {
        const labelWidth = 12.0;
        const seatGap = 6.0;
        const maxSeat = 26.0;
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
              _PickerSeat(
                size: seat,
                status: map.isMine(rowLabel, s)
                    ? _PickStatus.mine
                    : reserved.contains('$rowLabel:$s')
                        ? _PickStatus.reserved
                        : _PickStatus.available,
                onTap: busy ? null : () => onPick(rowLabel, s),
                semanticsLabel: '$rowLabel$s',
              ),
            ],
          ],
        );
      },
    );
  }
}

enum _PickStatus { mine, reserved, available }

class _PickerSeat extends StatelessWidget {
  const _PickerSeat({
    required this.status,
    required this.size,
    required this.semanticsLabel,
    this.onTap,
  });

  final _PickStatus status;
  final double size;
  final String semanticsLabel;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final Color fill;
    Border? border;
    switch (status) {
      case _PickStatus.mine:
        fill = SimfTokens.accent;
        border = Border.all(color: SimfTokens.beigeBorder);
      case _PickStatus.reserved:
        fill = SimfTokens.navy;
      case _PickStatus.available:
        fill = Colors.transparent;
        border = Border.all(color: SimfTokens.accent);
    }
    final box = Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: fill,
        border: border,
        borderRadius: BorderRadius.circular(3),
      ),
    );
    // Only available seats are tappable; reserved / own seats are inert.
    if (status != _PickStatus.available || onTap == null) {
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

class _PickerLegend extends StatelessWidget {
  const _PickerLegend({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.ltr,
      child: Wrap(
        alignment: WrapAlignment.center,
        spacing: SimfTokens.space2,
        runSpacing: SimfTokens.space2,
        children: <Widget>[
          _LegendSwatch(label: l10n.legendReserved, color: SimfTokens.navy),
          _LegendSwatch(
            label: l10n.legendAvailable,
            color: Colors.transparent,
            borderColor: SimfTokens.accent,
          ),
          _LegendSwatch(label: l10n.legendMine, color: SimfTokens.accent),
        ],
      ),
    );
  }
}

class _LegendSwatch extends StatelessWidget {
  const _LegendSwatch({
    required this.color,
    required this.label,
    this.borderColor,
  });

  final Color color;
  final String label;
  final Color? borderColor;

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
          width: 16,
          height: 16,
          decoration: BoxDecoration(
            color: color,
            border: borderColor != null ? Border.all(color: borderColor!) : null,
            borderRadius: BorderRadius.circular(3),
          ),
        ),
      ],
    );
  }
}
