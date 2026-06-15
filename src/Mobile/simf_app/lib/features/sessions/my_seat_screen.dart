import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import 'data/seat_map_models.dart';
import 'data/seat_map_repository.dart';

/// Page 018 — مقعدي · خريطة الجلوس · My Seat map (#18,
/// `/sessions/:sessionId/my-seat`, **auth-gated**, approved Visitor only).
///
/// One read (`GET /app/sessions/{id}/seats`) draws the whole hall grid: every
/// seat coloured by **derived** status — mine / reserved / available (Page_018
/// L-2) — with the caller's own seat highlighted. Read-only as drawn; the
/// interactive picker is a later mode over the same (existing) reserve endpoints
/// (L-4). Navigate opens the venue map (15); share opens the native sheet (E3).
/// UI is interim; final visuals come from SIMF-VID-001.
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
    return Scaffold(
      appBar: AppBar(leading: const SimfBackButton(), title: Text(l10n.mySeatMapTitle)),
      body: SafeArea(child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_notFound) {
      return _Message(icon: Icons.event_busy_outlined, text: l10n.sessionNotFound);
    }
    if (_error || _map == null) {
      return _ErrorState(message: l10n.seatMapError, onRetry: () => unawaited(_load()));
    }
    final map = _map!;
    if (!map.hasLayout) {
      return _Message(
        icon: Icons.event_seat_outlined,
        text: l10n.seatMapUnavailable,
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
    final reserved = map.reservedKeys();
    return ListView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        _Banner(map: map, l10n: l10n),
        const SizedBox(height: SimfTokens.space3),
        // The hall card (mockup `.hall`): a navy gradient panel holding the
        // gold stage band at the top and the seat rows below it.
        Container(
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space3,
            SimfTokens.space3,
            SimfTokens.space3,
            SimfTokens.space4,
          ),
          decoration: BoxDecoration(
            gradient: const LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              colors: <Color>[SimfTokens.line2, SimfTokens.surfaceTint],
            ),
            border: Border.all(color: SimfTokens.line2),
            borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
          ),
          child: Column(
            children: <Widget>[
              _StageBar(label: l10n.stageLabel),
              const SizedBox(height: SimfTokens.space3),
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: Directionality(
                  // The hall plan keeps the stage at the top and seat columns in
                  // venue order — do not mirror the grid geometry in RTL (L-7).
                  textDirection: TextDirection.ltr,
                  child: Column(
                    children: <Widget>[
                      for (final row in map.rowLabels)
                        _SeatRow(
                          rowLabel: row,
                          seatsPerRow: map.seatsPerRow,
                          reserved: reserved,
                          map: map,
                        ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: SimfTokens.space4),
        _Legend(l10n: l10n),
        const SizedBox(height: SimfTokens.space5),
        _Actions(l10n: l10n, onNavigate: onNavigate, onShare: onShare),
      ],
    );
  }
}

class _Banner extends StatelessWidget {
  const _Banner({required this.map, required this.l10n});

  final SessionSeatMap map;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final cell = map.myCell;
    return Card(
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        side: BorderSide(
          color: cell != null ? SimfTokens.accent : SimfTokens.line2,
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Row(
          children: <Widget>[
            Icon(
              cell != null ? Icons.event_seat : Icons.event_seat_outlined,
              color: cell != null ? SimfTokens.accent : SimfTokens.txtTertiary,
            ),
            const SizedBox(width: SimfTokens.space3),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    cell != null
                        ? l10n.seatLocation(cell.rowLabel, cell.seatNumber)
                        : l10n.noSeatYet,
                    style: const TextStyle(
                      color: SimfTokens.surface,
                      fontWeight: FontWeight.w700,
                      fontSize: SimfTokens.textMd,
                    ),
                  ),
                  const SizedBox(height: SimfTokens.space1),
                  Text(
                    l10n.seatCapacity(map.activeReservedCount, map.capacity),
                    style: const TextStyle(
                      color: SimfTokens.txtSecondary,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _StageBar extends StatelessWidget {
  const _StageBar({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(vertical: SimfTokens.space2),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: <Color>[
            SimfTokens.accent.withValues(alpha: 0.20),
            SimfTokens.accent.withValues(alpha: 0.06),
          ],
        ),
        border: Border.all(color: SimfTokens.accent),
        borderRadius: const BorderRadius.only(
          bottomLeft: Radius.circular(SimfTokens.radiusLarge),
          bottomRight: Radius.circular(SimfTokens.radiusLarge),
        ),
      ),
      child: Text(
        label,
        textAlign: TextAlign.center,
        style: const TextStyle(
          fontWeight: FontWeight.w700,
          letterSpacing: 2,
          fontSize: SimfTokens.textXs,
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
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        children: <Widget>[
          SizedBox(
            width: 22,
            child: Text(
              rowLabel,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textXs,
                color: SimfTokens.txtTertiary,
              ),
            ),
          ),
          for (var seat = 1; seat <= seatsPerRow; seat++)
            _SeatBox(
              status: map.isMine(rowLabel, seat)
                  ? _SeatStatus.mine
                  : reserved.contains('$rowLabel:$seat')
                      ? _SeatStatus.reserved
                      : _SeatStatus.available,
            ),
        ],
      ),
    );
  }
}

enum _SeatStatus { mine, reserved, available }

class _SeatBox extends StatelessWidget {
  const _SeatBox({required this.status});

  final _SeatStatus status;

  @override
  Widget build(BuildContext context) {
    // Mockup `.hall .r i`: plain seat squares (no number) — mine glows gold,
    // taken is a denser fill with no border, available is a faint fill.
    final Color fill;
    Border? border;
    List<BoxShadow>? glow;
    switch (status) {
      case _SeatStatus.mine:
        fill = SimfTokens.accent;
        border = Border.all(color: SimfTokens.accent);
        glow = <BoxShadow>[
          BoxShadow(
            color: SimfTokens.accent.withValues(alpha: 0.30),
            blurRadius: 0,
            spreadRadius: 2,
          ),
          BoxShadow(
            color: SimfTokens.accent.withValues(alpha: 0.55),
            blurRadius: 8,
          ),
        ];
      case _SeatStatus.reserved:
        fill = SimfTokens.line;
      case _SeatStatus.available:
        fill = SimfTokens.surfaceTint;
        border = Border.all(color: SimfTokens.line2);
    }
    return Container(
      width: 14,
      height: 14,
      margin: const EdgeInsets.symmetric(horizontal: 2, vertical: 3),
      decoration: BoxDecoration(
        color: fill,
        border: border,
        boxShadow: glow,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
    );
  }
}

class _Legend extends StatelessWidget {
  const _Legend({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      alignment: WrapAlignment.center,
      spacing: SimfTokens.space4,
      runSpacing: SimfTokens.space2,
      children: <Widget>[
        _LegendItem(color: SimfTokens.accent, label: l10n.legendMine),
        _LegendItem(
          color: SimfTokens.surfaceTint,
          border: true,
          label: l10n.legendAvailable,
        ),
        _LegendItem(color: SimfTokens.line, label: l10n.legendReserved),
      ],
    );
  }
}

class _LegendItem extends StatelessWidget {
  const _LegendItem({required this.color, required this.label, this.border = false});

  final Color color;
  final String label;
  final bool border;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Container(
          width: 14,
          height: 14,
          decoration: BoxDecoration(
            color: color,
            border: border ? Border.all(color: SimfTokens.line2) : null,
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
        ),
        const SizedBox(width: SimfTokens.space1),
        Text(
          label,
          style: const TextStyle(
            color: SimfTokens.txtSecondary,
            fontSize: SimfTokens.textSm,
          ),
        ),
      ],
    );
  }
}

class _Actions extends StatelessWidget {
  const _Actions({required this.l10n, required this.onNavigate, this.onShare});

  final AppL10n l10n;
  final VoidCallback onNavigate;
  final VoidCallback? onShare;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: FilledButton(
            onPressed: onNavigate,
            child: Text(l10n.navigateToSeat),
          ),
        ),
        const SizedBox(width: SimfTokens.space3),
        Expanded(
          child: OutlinedButton(
            onPressed: onShare,
            child: Text(l10n.shareLocation),
          ),
        ),
      ],
    );
  }
}

class _Message extends StatelessWidget {
  const _Message({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(icon, size: 56, color: SimfTokens.txtTertiary),
          const SizedBox(height: SimfTokens.space3),
          Text(text, style: const TextStyle(color: SimfTokens.txtSecondary)),
        ],
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
          ],
        ),
      ),
    );
  }
}
