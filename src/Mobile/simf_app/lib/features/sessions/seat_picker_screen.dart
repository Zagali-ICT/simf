import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_confirm_dialog.dart';
import 'package:simf_app/app/widgets/simf_info_dialog.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/sessions/data/my_reservation.dart';
import 'package:simf_app/features/sessions/data/seat_enums.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/seat_map_repository.dart';
import 'package:simf_app/features/sessions/widgets/seat_map_async_view.dart';
import 'package:simf_app/features/sessions/widgets/seat_picker_body.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Seat picker — اختيار المقعد · route: `RouteNames.seatPicker`
///
/// Contract (A8, owner 2026-07-27): there is **no approval step** — bookings
/// are reservation-only, so the reservation is created Approved and the seat
/// is held the moment it is confirmed. The server's pre-start sweep releases
/// it if the visitor has not checked in three minutes before the start.
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
  /// seat-specific reservation for this session. An open-seating join has no
  /// seat to move, so it keeps the ordinary reserve behaviour.
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
      // Deliberately NOT the screen CTA's own wording: the dialog's action
      // reads "تغيير المقعد" so the two buttons on screen are never the same
      // words.
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
      failureMessage = failure.localizedMessage(l10n).trim();
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
      // A full session (the capacity cap the CP enforces) gets its own message
      // so a random/auto pick that hits the maximum reads "no places remain"
      // instead of a generic failure. Other errors keep the generic
      // seat-reserve message. D-771 — the two tier refusals get their own copy
      // so a visitor who somehow reaches an ineligible seat is told WHY, not
      // "could not reserve". B1 — a losing move says the visitor KEPT their
      // seat (the server rolled the whole move back), and the remaining move
      // refusals (session started, same seat, no seat) surface the backend's
      // own bilingual reason.
      final message = switch (failureCode) {
        'SEAT_SESSION_FULL' => l10n.joinSessionFull,
        'SEAT_TIER_RESERVED' => l10n.seatTierVvipLocked,
        'SEAT_TIER_NOT_ELIGIBLE' => l10n.seatTierVipLocked,
        'SEAT_ALREADY_RESERVED' when moving => l10n.seatChangeTaken,
        _ when moving =>
          failureMessage.isNotEmpty ? failureMessage : l10n.seatChangeFailed,
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
    final held = _heldSeat(value.value);
    return SimfPageShell(
      title: held == null ? l10n.seatPickerTitle : l10n.seatChangeTitle,
      onBack: () => backOrHome(context),
      tab: SimfTab.sessions,
      body: SeatMapAsyncView(
        value: value,
        onRefresh: () =>
            refreshAsync(ref, seatMapProvider(widget.sessionId).future),
        builder: (map) {
          final held = _heldSeat(map);
          return SeatPickerBody(
            map: map,
            held: held,
            l10n: l10n,
            busy: _busy,
            selectedRow: _selectedRow,
            selectedSeat: _selectedSeat,
            onSeatTap: _select,
            onConfirm: () => unawaited(
              held == null
                  ? _reserve(l10n, _selectedRow!, _selectedSeat!)
                  : _move(l10n, held, _selectedRow!, _selectedSeat!),
            ),
            onRandom: () => unawaited(_reserveRandom(l10n)),
          );
        },
      ),
    );
  }
}
