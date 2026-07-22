import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../app/widgets/simf_info_dialog.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'data/seat_map_models.dart';
import 'data/seat_map_repository.dart';
import 'widgets/hall_seat_map.dart';
import 'widgets/seat_map_async_view.dart';

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
  bool _busy = false;

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
    String? failureCode;
    try {
      await action(ref.read(seatMapRepositoryProvider));
      reserved = true;
    } on ApiFailure catch (failure) {
      // Reported below once we know the screen is still mounted.
      failureCode = failure.code;
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
    if (!mounted) {
      return;
    }
    if (reserved) {
      // D-750 — a one-button info alert (replaces the old seatReservedToast
      // snackbar) explaining the 3-minute pre-start check-in hold rule; on
      // dismiss, pop back to the session page (true → it reloads).
      await SimfInfoDialog.show(context, title: l10n.seatReservedAlertBody);
      if (!mounted) {
        return;
      }
      navigator.pop(true);
    } else {
      // A full session (the capacity cap the CP enforces) gets its own message so
      // a random/auto pick that hits the maximum reads "no places remain" instead
      // of a generic failure. Other errors keep the generic seat-reserve message.
      final message = failureCode == 'SEAT_SESSION_FULL'
          ? l10n.joinSessionFull
          : l10n.seatReserveFailed;
      messenger.showSnackBar(SnackBar(content: Text(message)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final value = ref.watch(seatMapProvider(widget.sessionId));
    return SimfPageShell(
      title: l10n.seatPickerTitle,
      onBack: () => backOrHome(context),
      tab: SimfTab.sessions,
      body: SeatMapAsyncView(
        value: value,
        onRetry: () => ref.invalidate(seatMapProvider(widget.sessionId)),
        builder: (map) => _picker(l10n, map),
      ),
    );
  }

  Widget _picker(AppL10n l10n, SessionSeatMap map) {
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
            style: SimfTokens.labelWhiteBoldTitle,
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            l10n.seatPickerHint,
            textAlign: TextAlign.center,
            style: SimfTokens.labelBeigeSm,
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
            maxSeatSize: SimfTokens.seatCapPicker,
            availableBorderColor: SimfTokens.accent,
            swatchSize: SimfTokens.seatSwatchLg,
          ),
          const SizedBox(height: SimfTokens.space5),
          FilledButton.icon(
            onPressed: _busy ? null : () => unawaited(_reserveRandom(l10n)),
            style: FilledButton.styleFrom(
              minimumSize: const Size.fromHeight(SimfTokens.controlHeight),
              backgroundColor: SimfTokens.accent,
              foregroundColor: SimfTokens.surface,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              ),
            ),
            icon: const Icon(Icons.shuffle, size: 20),
            label: Text(
              l10n.seatPickerRandomCta,
              style: SimfTokens.titleBold,
            ),
          ),
        ],
      ),
    );
  }
}
