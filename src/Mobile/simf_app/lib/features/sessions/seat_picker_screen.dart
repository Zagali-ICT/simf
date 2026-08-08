import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../app/widgets/simf_confirm_dialog.dart';
import '../../app/widgets/simf_info_dialog.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../core/utils/refresh.dart';
import 'data/seat_map_models.dart';
import 'data/seat_map_repository.dart';
import 'widgets/hall_seat_map.dart';
import 'widgets/seat_map_async_view.dart';
import 'widgets/selected_seat_chip.dart';

/// D-485 — **Seat picker** (`/sessions/:sessionId/pick-seat`, approved Visitor).
/// An assigned-seat session's selectable hall grid: tap an **available** seat to
/// SELECT it (a confirmation chip appears), then commit with "Confirm my seat";
/// or auto-pick one (owner 2026-07-25 — a two-step select→confirm replaces the
/// former one-tap reserve). Reuses the shipped seat endpoints (`GET …/seats`
/// to draw, `POST …/seats/reserve` / `…/reserve-random` to hold). It pops
/// with `true` so the session page reloads and shows the reservation.
///
/// A8 (2026-07-27) — there is **no approval step**: the owner made bookings
/// reservation-only on 2026-07-18, so the reservation is created **Approved**
/// and the seat is held the moment it is confirmed. It stays a provisional
/// hold — the server's pre-start sweep releases it if the visitor has not
/// checked in by three minutes before the session starts, which is what the
/// success dialog says.
///
/// **B1 — change seat (owner request).** The same screen is the destination
/// chooser for a seat CHANGE: when the seat map says the caller already holds a
/// seat (`myCell`, seat-specific) it opens in **change mode** — its own title,
/// hint and CTA, no auto-pick (a move is deliberate) — and committing goes through
/// a confirm step that names the OLD and the NEW seat before calling
/// `POST …/seats/move`. The move is atomic server-side, so a lost race leaves the
/// visitor on the seat they already had; the picker says so and stays usable.
class SeatPickerScreen extends ConsumerStatefulWidget {
  const SeatPickerScreen({required this.sessionId, super.key});

  final String sessionId;

  @override
  ConsumerState<SeatPickerScreen> createState() => _SeatPickerScreenState();
}

class _SeatPickerScreenState extends ConsumerState<SeatPickerScreen> {
  bool _busy = false;
  // The seat the visitor has tapped but not yet confirmed (null = none).
  String? _selectedRow;
  int? _selectedSeat;

  void _select(String row, int seat) {
    setState(() {
      _selectedRow = row;
      _selectedSeat = seat;
    });
  }

  /// B1 — the picker is in CHANGE mode when the caller already holds a
  /// seat-specific reservation for this session. An open-seating join has no seat
  /// to move, so it keeps the ordinary reserve behaviour.
  static SeatCell? _heldSeat(SessionSeatMap? map) {
    final cell = map?.myCell;
    if (cell == null || cell.kind == SeatReservationKind.openSeating) {
      return null;
    }
    return cell;
  }

  Future<void> _reserve(AppL10n l10n, String row, int seat) => _hold(
        l10n,
        (repo) =>
            repo.reserveSeat(widget.sessionId, rowLabel: row, seatNumber: seat),
        row,
        seat,
      );

  Future<void> _reserveRandom(AppL10n l10n) =>
      _hold(l10n, (repo) => repo.reserveRandom(widget.sessionId), null, null);

  /// B1 — commit a seat CHANGE: confirm first, naming both the seat being left
  /// and the seat being taken, then call the atomic move endpoint.
  Future<void> _move(AppL10n l10n, SeatCell from, String row, int seat) async {
    if (_busy) {
      return;
    }
    final confirmed = await SimfConfirmDialog.show(
      context,
      title: l10n.seatChangeConfirmTitle,
      message: l10n.seatChangeConfirmBody(
        from.rowLabel,
        from.seatNumber,
        row,
        seat,
      ),
      // Deliberately NOT the screen CTA's own wording: the dialog's action reads
      // "تغيير المقعد" so the two buttons on screen are never the same words.
      confirmLabel: l10n.seatChangeCta,
    );
    if (!confirmed || !mounted) {
      return;
    }
    await _hold(
      l10n,
      (repo) =>
          repo.moveSeat(widget.sessionId, rowLabel: row, seatNumber: seat),
      row,
      seat,
      moving: true,
    );
  }

  /// Shared hold flow: guard against a double-tap, call the endpoint, then —
  /// only if still mounted — toast the outcome and (on success) pop with `true`
  /// so the session page reloads. `_busy` is reset in a `finally` so any throw
  /// (not just ApiFailure) un-freezes the grid; the post-await navigation is
  /// `mounted`-guarded so backing out mid-request can't pop an unrelated route.
  /// [row]/[seat] are the committed seat (null for the auto-pick, whose seat the
  /// server chooses) and only feed the success copy.
  Future<void> _hold(
    AppL10n l10n,
    Future<MyReservation> Function(SeatMapRepository repo) action,
    String? row,
    int? seat, {
    bool moving = false,
  }) async {
    if (_busy) {
      return;
    }
    setState(() => _busy = true);
    final messenger = ScaffoldMessenger.of(context);
    final navigator = Navigator.of(context);
    var reserved = false;
    String? failureCode;
    var failureMessage = '';
    try {
      await action(ref.read(seatMapRepositoryProvider));
      reserved = true;
    } on ApiFailure catch (failure) {
      // Reported below once we know the screen is still mounted.
      failureCode = failure.code;
      failureMessage = failure.message.trim();
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
      // dismiss, pop back to the session page (true → it reloads). B1 — a move
      // keeps that hold rule from the seat it replaces, so it only confirms the
      // new location.
      await SimfInfoDialog.show(
        context,
        title: moving && row != null && seat != null
            ? l10n.seatChangedAlertBody(row, seat)
            : l10n.seatReservedAlertBody,
      );
      if (!mounted) {
        return;
      }
      navigator.pop(true);
    } else {
      // A full session (the capacity cap the CP enforces) gets its own message so
      // a random/auto pick that hits the maximum reads "no places remain" instead
      // of a generic failure. Other errors keep the generic seat-reserve message.
      // D-771 — the two tier refusals get their own copy so a visitor who somehow
      // reaches an ineligible seat is told WHY, not "could not reserve".
      // B1 — a losing move says the visitor KEPT their seat (the server rolled the
      // whole move back), and the remaining move refusals (session started, same
      // seat, no seat) surface the backend's own bilingual reason.
      final message = switch (failureCode) {
        'SEAT_SESSION_FULL' => l10n.joinSessionFull,
        'SEAT_TIER_RESERVED' => l10n.seatTierVvipLocked,
        'SEAT_TIER_NOT_ELIGIBLE' => l10n.seatTierVipLocked,
        'SEAT_ALREADY_RESERVED' when moving => l10n.seatChangeTaken,
        _ when moving => failureMessage.isNotEmpty
            ? failureMessage
            : l10n.seatChangeFailed,
        _ => l10n.seatReserveFailed,
      };
      messenger.showSnackBar(SnackBar(content: Text(message)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final value = ref.watch(seatMapProvider(widget.sessionId));
    // The shell's title is built before the body, so read the already-resolved
    // map (null while loading) to decide between "pick" and "change".
    final held = _heldSeat(value.valueOrNull);
    return SimfPageShell(
      title: held == null ? l10n.seatPickerTitle : l10n.seatChangeTitle,
      onBack: () => backOrHome(context),
      tab: SimfTab.sessions,
      body: SeatMapAsyncView(
        value: value,
        onRefresh: () =>
            refreshAsync(ref, seatMapProvider(widget.sessionId).future),
        builder: (map) => _picker(l10n, map),
      ),
    );
  }

  Widget _picker(AppL10n l10n, SessionSeatMap map) {
    final held = _heldSeat(map);
    return Directionality(
      textDirection: TextDirection.rtl,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space2,
          SimfTokens.space4,
          SimfTokens.space5,
        ),
        children: <Widget>[
          Text(
            map.localizedSessionTitle(l10n.isArabic) ??
                (held == null ? l10n.seatPickerTitle : l10n.seatChangeTitle),
            textAlign: TextAlign.center,
            style: SimfTokens.labelWhiteBoldTitle,
          ),
          const SizedBox(height: SimfTokens.space2),
          Text(
            held == null ? l10n.seatPickerHint : l10n.seatChangeHint,
            textAlign: TextAlign.center,
            style: SimfTokens.labelBeigeSm,
          ),
          // B1 — in change mode, name the seat being left so the destination is
          // always chosen against a visible "from".
          if (held != null) ...<Widget>[
            const SizedBox(height: SimfTokens.space2),
            Text(
              l10n.seatLocation(held.rowLabel, held.seatNumber),
              textAlign: TextAlign.center,
              style: SimfTokens.labelWhiteSemibold,
            ),
          ],
          // D-771 — explain the padlocked seats, but only for a tiered hall so a
          // plain hall keeps the shipped copy unchanged.
          if (map.hasTiers) ...<Widget>[
            const SizedBox(height: SimfTokens.space2),
            Text(
              l10n.seatTierPickerHint,
              textAlign: TextAlign.center,
              style: SimfTokens.labelBeigeSm,
            ),
          ],
          const SizedBox(height: SimfTokens.space5),
          // The shared hall card in its selectable configuration: available
          // seats tappable with a gold border cue, 26px seat cap, 16px legend
          // swatches (the picker's pre-consolidation render, D-600). A tap
          // SELECTS (highlights) the seat; the Confirm CTA below commits it.
          HallSeatMapCard(
            map: map,
            l10n: l10n,
            busy: _busy,
            onSeatTap: _select,
            selectedRowLabel: _selectedRow,
            selectedSeatNumber: _selectedSeat,
            maxSeatSize: SimfTokens.seatCapPicker,
            availableBorderColor: SimfTokens.accent,
            swatchSize: SimfTokens.seatSwatchLg,
          ),
          const SizedBox(height: SimfTokens.space5),
          if (_selectedRow != null && _selectedSeat != null) ...<Widget>[
            SelectedSeatChip(
              label:
                  l10n.seatPickerSelectedChip(_selectedRow!, _selectedSeat!),
            ),
            const SizedBox(height: SimfTokens.space4),
          ],
          FilledButton.icon(
            onPressed: (_busy || _selectedRow == null || _selectedSeat == null)
                ? null
                : () => unawaited(
                      held == null
                          ? _reserve(l10n, _selectedRow!, _selectedSeat!)
                          : _move(l10n, held, _selectedRow!, _selectedSeat!),
                    ),
            style: FilledButton.styleFrom(
              minimumSize: const Size.fromHeight(SimfTokens.controlHeight),
              backgroundColor: SimfTokens.accent,
              foregroundColor: SimfTokens.surface,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              ),
            ),
            icon: const Icon(Icons.event_seat, size: SimfTokens.seatPickerScreenSize),
            label: Text(
              held == null
                  ? l10n.seatPickerConfirmCta
                  : l10n.seatChangeConfirmCta,
              style: SimfTokens.titleBold,
            ),
          ),
          // B1 — no auto-pick when changing seats: a move is a deliberate choice of
          // WHERE to go, and a random destination would be a worse seat lottery.
          if (held == null) ...<Widget>[
            const SizedBox(height: SimfTokens.space4),
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
              icon: const Icon(Icons.shuffle, size: SimfTokens.seatPickerScreenSize),
              label: Text(
                l10n.seatPickerRandomCta,
                style: SimfTokens.titleBold,
              ),
            ),
          ],
        ],
      ),
    );
  }
}

