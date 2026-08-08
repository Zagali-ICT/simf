import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../app/widgets/simf_scanner_body.dart';
import '../../core/responsive/breakpoints.dart';
import '../../core/responsive/max_width_body.dart';
import '../../core/utils/refresh.dart';
import '../sessions/data/seat_map_models.dart';
import '../sessions/data/seat_map_repository.dart';
import '../sessions/widgets/hall_seat_map.dart';
import '../sessions/widgets/seat_map_async_view.dart';
import 'data/staff_seating_repository.dart';
import 'data/staff_seating_models.dart';

/// D-771 (owner 2026-07-26) — the **staff seating desk**
/// (`/staff/seating/:sessionId`, approved Staff). Derived from the visitor seat
/// picker (`SeatPickerScreen`): the same shared `HallSeatMapCard` hall grid, but
/// tapping a seat ASKS WHO SITS THERE instead of selecting it, and a badge
/// scanner above the grid answers the opposite question — where does this guest
/// sit.
///
/// * Role-gated to `AppRole.staff` in `router.dart` (route 118), exactly like the
///   gate scanner (105); the server independently enforces the `Seating.Assist`
///   operational permission that the Staff app role carries (D-563), so the app
///   gate is only a UX guard.
/// * Tablet-responsive through the `WindowSize` API + `MaxWidthBody` (no
///   hardcoded breakpoints): a compact phone stacks the scanner above the grid,
///   a tablet puts the scanner and the result card side by side.
/// * The guest photo is fetched through the authenticated Dio bytes path
///   (D-422) — never `Image.network`, which cannot carry the bearer token.
/// * Bilingual AR + EN with correct RTL; every control carries an accessible
///   name.
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
    // a compact phone stacks them. Bucket comes from the shared WindowSize API —
    // never a hardcoded pixel test.
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
              map.localizedSessionTitle(l10n.isArabic) ?? l10n.staffSeatingTitle,
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

  /// (a) the shared scanner body — the same viewfinder + manual-entry field every
  /// other SIMF scanner uses (D-737), so the desk needs no bespoke reader.
  Widget _scannerCard(AppL10n l10n) {
    return _DeskCard(
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
      return _DeskCard(
        child: Text(_error!, style: SimfTokens.labelWhiteSemibold),
      );
    }
    final result = _result;
    if (result == null) {
      return _DeskCard(
        child: Text(l10n.staffSeatingIntro, style: SimfTokens.labelBeigeSm),
      );
    }
    return _DeskCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          _OccupantHeader(result: result, photo: _photo, l10n: l10n),
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
              _DeskRow(
                label: l10n.staffSeatingSeat,
                value: l10n.staffSeatingSeatValue(
                  result.rowLabel!,
                  result.seatNumber!,
                ),
              ),
            if (result.reservationId != null)
              _DeskRow(
                label: l10n.staffSeatingReference,
                value: result.reservationId!,
              ),
            _DeskRow(
              label: l10n.staffSeatingGuest,
              // A VVIP protocol seat has no registration: the administrator's
              // manual note IS the occupant record.
              value: result.localizedGuestHint(l10n.isArabic) ??
                  result.localizedName(l10n.isArabic),
            ),
            _DeskRow(
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
  /// configuration — a tap resolves the seat's occupant rather than selecting it.
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

/// The desk's navy surface — one shared card so the scanner, the result and the
/// error state never drift apart.
class _DeskCard extends StatelessWidget {
  const _DeskCard({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: child,
    );
  }
}

/// One label / value line in the result card.
class _DeskRow extends StatelessWidget {
  const _DeskRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: SimfTokens.space2),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text('$label: ', style: SimfTokens.labelBeigeSm),
          Expanded(
            child: Text(value, style: SimfTokens.labelWhiteSemibold),
          ),
        ],
      ),
    );
  }
}

/// The guest's photo + name. The bytes arrive from the authenticated Dio path
/// (D-422); until they do (or when there is no photo) a labelled placeholder
/// keeps the row's height stable and the a11y tree named.
class _OccupantHeader extends StatelessWidget {
  const _OccupantHeader({
    required this.result,
    required this.photo,
    required this.l10n,
  });

  final StaffSeatOccupant result;
  final Uint8List? photo;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final bytes = photo;
    return Row(
      children: <Widget>[
        Semantics(
          image: true,
          label: l10n.staffSeatingGuestPhoto,
          child: ClipRRect(
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            child: SizedBox(
              width: SimfTokens.staffSeatingPhotoSize,
              height: SimfTokens.staffSeatingPhotoSize,
              child: bytes == null
                  ? const ColoredBox(
                      color: SimfTokens.navy,
                      child: Icon(
                        Icons.person_outline,
                        color: SimfTokens.beigeBorder,
                      ),
                    )
                  : Image.memory(bytes, fit: BoxFit.cover),
            ),
          ),
        ),
        const SizedBox(width: SimfTokens.space3),
        Expanded(
          child: Text(
            result.localizedGuestHint(l10n.isArabic) ??
                result.localizedName(l10n.isArabic),
            style: SimfTokens.labelWhiteBoldTitle,
          ),
        ),
      ],
    );
  }
}
