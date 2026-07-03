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
import 'widgets/hall_seat_map.dart';

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
          // The shared hall card in its selectable configuration: available
          // seats tappable with a gold border cue, 26px seat cap, 16px legend
          // swatches (the picker's pre-consolidation render, D-600).
          HallSeatMapCard(
            map: map,
            l10n: l10n,
            busy: _busy,
            onSeatTap: (row, seat) => unawaited(_reserve(l10n, row, seat)),
            maxSeatSize: 26,
            availableBorderColor: SimfTokens.accent,
            swatchSize: 16,
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
