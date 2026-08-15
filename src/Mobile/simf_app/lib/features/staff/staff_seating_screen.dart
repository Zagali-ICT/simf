import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/app/widgets/simf_scanner_body.dart';
import 'package:simf_app/core/responsive/breakpoints.dart';
import 'package:simf_app/core/responsive/max_width_body.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/seat_map_repository.dart';
import 'package:simf_app/features/sessions/widgets/hall_seat_map.dart';
import 'package:simf_app/features/sessions/widgets/seat_map_async_view.dart';
import 'package:simf_app/features/staff/data/staff_seating_models.dart';
import 'package:simf_app/features/staff/data/staff_seating_repository.dart';
import 'package:simf_app/features/staff/widgets/desk_card.dart';
import 'package:simf_app/features/staff/widgets/desk_row.dart';
import 'package:simf_app/features/staff/widgets/occupant_header.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// D-771 (owner 2026-07-26) — the **staff seating desk**
/// (`/staff/seating/:sessionId`, approved Staff). Derived from the visitor seat
/// picker (`SeatPickerScreen`): the same shared `HallSeatMapCard` hall grid,
/// but tapping a seat ASKS WHO SITS THERE instead of selecting it, and a badge
/// scanner above the grid answers the opposite question — where does this guest
/// sit.
///
/// * Role-gated to `AppRole.staff` in `router.dart` (route 118), exactly like
///   the gate scanner (105); the server independently enforces the
///   `Seating.Assist` operational permission that the Staff app role carries
///   (D-563), so the app gate is only a UX guard.
/// * Tablet-responsive through the `WindowSize` API + `MaxWidthBody` (no
///   hardcoded breakpoints): a compact phone stacks the scanner above the grid,
///   a tablet puts the scanner and the result card side by side.
/// * The guest photo is fetched through the authenticated Dio bytes path
///   (D-422) — never `Image.network`, which cannot carry the bearer token.
/// * Bilingual AR + EN with correct RTL; every control carries an accessible
///   name.
///
/// Route: `RouteNames.staffSeating`.
/// Data: [seatMapProvider], [staffSeatingRepositoryProvider].
/// Perf: ListView builds every child up front — correct for a short static
///       page, a defect on a data feed.
class StaffSeatingScreen extends ConsumerStatefulWidget {
  const StaffSeatingScreen({required this.sessionId, super.key});

  final String sessionId;

  @override
  ConsumerState<StaffSeatingScreen> createState() => _StaffSeatingScreenState();
}

class _StaffSeatingScreenState extends ConsumerState<StaffSeatingScreen> {
  StaffSeatOccupant? _result;
  Uint8List? _photo;
  bool _busy = false;
  String? _error;

  /// (b) Tap a seat → who sits there. Shows the reservation reference, the
  /// guest's name + photo, or — for a VVIP protocol seat — the administrator's
  /// manual guest note.
  Future<void> _lookupSeat(String rowLabel, int seatNumber) => _lookup(
        (repo) => repo.lookupSeat(
          widget.sessionId,
          rowLabel: rowLabel,
          seatNumber: seatNumber,
        ),
      );

  /// (a) Scan/type a badge QR → where does this guest sit.
  Future<void> _lookupBadge(String qrId) =>
      _lookup((repo) => repo.lookupByBadge(widget.sessionId, qrId));

  /// Shared lookup flow: single-flight, then load the photo (when there is one)
  /// through the authenticated bytes path. `_busy` is cleared in a `finally` so
  /// any throw un-freezes the desk, and every post-await `setState` is
  /// `mounted`-guarded so backing out mid-request is safe.
  Future<void> _lookup(
    Future<StaffSeatOccupant> Function(StaffSeatingRepository repo) action,
  ) async {
    if (_busy) {
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    final repo = ref.read(staffSeatingRepositoryProvider);
    StaffSeatOccupant? found;
    String? failureCode;
    try {
      found = await action(repo);
    } on ApiFailure catch (failure) {
      failureCode = failure.code;
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
    if (!mounted) {
      return;
    }
    if (found == null) {
      final l10n = AppL10n.of(context);
      setState(() {
        _result = null;
        _photo = null;
        _error = failureCode == 'ATTENDEE_QR_UNKNOWN'
            ? l10n.staffSeatingUnknownBadge
            : l10n.staffSeatingLookupFailed;
      });
      return;
    }
    setState(() {
      _result = found;
      _photo = null;
    });
    if (found.hasPhoto && found.userId != null) {
      final bytes = await repo.occupantPhoto(widget.sessionId, found.userId!);
      if (mounted) {
        setState(() => _photo = bytes);
      }
    }
  }

  void _clear() {
    setState(() {
      _result = null;
      _photo = null;
      _error = null;
    });
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final value = ref.watch(seatMapProvider(widget.sessionId));
    return SimfPageShell(
      title: l10n.staffSeatingTitle,
      onBack: () => backOrHome(context),
      tab: SimfTab.sessions,
      body: SeatMapAsyncView(
        value: value,
        onRefresh: () =>
            refreshAsync(ref, seatMapProvider(widget.sessionId).future),
        builder: (map) => _desk(l10n, map),
      ),
    );
  }

  Widget _desk(AppL10n l10n, SessionSeatMap map) {
    // Tablet (medium and wider) puts the scanner + result beside the hall plan;
    // a compact phone stacks them. Bucket comes from the shared WindowSize API
    // — never a hardcoded pixel test.
    final wide = !WindowSize.of(context).isCompact;
    final scanner = _scannerCard(l10n);
    final result = _resultCard(l10n);
    final plan = _planCard(l10n, map);
    return Directionality(
      textDirection: l10n.isArabic ? TextDirection.rtl : TextDirection.ltr,
      child: MaxWidthBody(
        maxWidth: SimfTokens.staffSeatingMaxWidth,
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space2,
          SimfTokens.space4,
          SimfTokens.space5,
        ),
        child: ListView(
          children: <Widget>[
            Text(
              map.localizedSessionTitle(isArabic: l10n.isArabic) ??
                  l10n.staffSeatingTitle,
              textAlign: TextAlign.center,
              style: SimfTokens.labelWhiteBoldTitle,
            ),
            const SizedBox(height: SimfTokens.space2),
            Text(
              l10n.staffSeatingIntro,
              textAlign: TextAlign.center,
              style: SimfTokens.labelBeigeSm,
            ),
            const SizedBox(height: SimfTokens.space5),
            if (wide)
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Expanded(child: scanner),
                  const SizedBox(width: SimfTokens.space4),
                  Expanded(child: result),
                ],
              )
            else ...<Widget>[
              scanner,
              const SizedBox(height: SimfTokens.space4),
              result,
            ],
            const SizedBox(height: SimfTokens.space5),
            plan,
          ],
        ),
      ),
    );
  }

  /// (a) the shared scanner body — the same viewfinder + manual-entry field
  /// every other SIMF scanner uses (D-737), so the desk needs no bespoke
  /// reader.
  Widget _scannerCard(AppL10n l10n) {
    return DeskCard(
      child: SimfScannerBody(
        fieldLabel: l10n.staffSeatingScanLabel,
        continueLabel: l10n.staffSeatingScanCta,
        bottomHint: l10n.staffSeatingScanHint,
        onCode: _lookupBadge,
      ),
    );
  }

  /// The single result card both lookups render into: the "no result" hint, the
  /// error, or the occupant (reference id, name, photo, VVIP guest note).
  Widget _resultCard(AppL10n l10n) {
    if (_error != null) {
      return DeskCard(
        child: Text(_error!, style: SimfTokens.labelWhiteSemibold),
      );
    }
    final result = _result;
    if (result == null) {
      return DeskCard(
        child: Text(l10n.staffSeatingIntro, style: SimfTokens.labelBeigeSm),
      );
    }
    return DeskCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          OccupantHeader(result: result, photo: _photo, l10n: l10n),
          const SizedBox(height: SimfTokens.space3),
          if (!result.found)
            Text(
              result.rowLabel == null
                  ? l10n.staffSeatingNoSeat
                  : l10n.staffSeatingSeatEmpty,
              style: SimfTokens.labelWhiteSemibold,
            )
          else ...<Widget>[
            if (result.rowLabel != null && result.seatNumber != null)
              DeskRow(
                label: l10n.staffSeatingSeat,
                value: l10n.staffSeatingSeatValue(
                  result.rowLabel!,
                  result.seatNumber!,
                ),
              ),
            if (result.reservationId != null)
              DeskRow(
                label: l10n.staffSeatingReference,
                value: result.reservationId!,
              ),
            DeskRow(
              label: l10n.staffSeatingGuest,
              // A VVIP protocol seat has no registration: the administrator's
              // manual note IS the occupant record.
              value: result.localizedGuestHint(isArabic: l10n.isArabic) ??
                  result.localizedName(isArabic: l10n.isArabic),
            ),
            DeskRow(
              label: _tierLabel(l10n, result.tier),
              value: result.checkedIn
                  ? l10n.staffSeatingCheckedIn
                  : l10n.staffSeatingNotCheckedIn,
            ),
          ],
          const SizedBox(height: SimfTokens.space3),
          TextButton(
            onPressed: _busy ? null : _clear,
            child: Text(
              l10n.staffSeatingClear,
              style: SimfTokens.labelBeigeSm,
            ),
          ),
        ],
      ),
    );
  }

  /// (b) the hall plan. The SHARED `HallSeatMapCard` in its tappable
  /// configuration — a tap resolves the seat's occupant rather than selecting
  /// it.
  Widget _planCard(AppL10n l10n, SessionSeatMap map) {
    return HallSeatMapCard(
      map: map,
      l10n: l10n,
      busy: _busy,
      onSeatTap: (row, seat) => unawaited(_lookupSeat(row, seat)),
      maxSeatSize: SimfTokens.seatCapPicker,
      availableBorderColor: SimfTokens.accent,
      swatchSize: SimfTokens.seatSwatchLg,
      // The desk must be able to inspect EVERY seat — reserved, own, VVIP and
      // VIP alike — because it is looking up occupants, not reserving.
      inspectMode: true,
    );
  }

  static String _tierLabel(AppL10n l10n, SeatTier tier) => switch (tier) {
        SeatTier.vvip => l10n.seatTierVvip,
        SeatTier.vip => l10n.seatTierVip,
        SeatTier.normal => l10n.seatTierNormal,
      };
}
