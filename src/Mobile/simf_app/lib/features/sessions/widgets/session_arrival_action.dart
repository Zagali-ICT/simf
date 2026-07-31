import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../core/location/device_location.dart';
import '../../../core/utils/saudi_time.dart';
import '../data/hall_attendance_repository.dart';

/// FR-305 / FR-506 (D-241) — the attendee's own hall check-in on the session
/// detail: an "أنا هنا / I'm here" button that reports the device position to
/// `POST /app/sessions/{id}/arrival` and renders the returned
/// [HallAttendanceStatus].
///
/// The SERVER decides: it checks the reported point against the session hall's
/// geofence and either opens an attendance row or refuses with a coded error.
/// The raw coordinates are never persisted (FDS-003 §10) — only the derived
/// enter/leave instants come back.
///
/// **Inert by design until a hall is given a boundary.** A hall with no
/// lat/lon/radius answers `HALL_GEOFENCE_NOT_CONFIGURED`, which renders as a
/// plain "no boundary set yet" message, not an error — that is the expected
/// state until the Control Panel geofence page is populated (owner Q6,
/// 2026-07-30). The device fix itself comes from the overridable
/// [deviceLocationProvider] seam.
class SessionArrivalAction extends ConsumerStatefulWidget {
  const SessionArrivalAction({
    required this.sessionId,
    required this.l10n,
    super.key,
  });

  final String sessionId;
  final AppL10n l10n;

  @override
  ConsumerState<SessionArrivalAction> createState() =>
      _SessionArrivalActionState();
}

class _SessionArrivalActionState extends ConsumerState<SessionArrivalAction> {
  bool _busy = false;
  HallAttendanceStatus? _status;

  @override
  Widget build(BuildContext context) {
    final l10n = widget.l10n;
    // The server-held state leads; a local result from this session's own tap
    // overrides it without a refetch.
    final loaded = ref.watch(hallAttendanceStatusProvider(widget.sessionId));
    final status = _status ?? loaded.valueOrNull;
    final arrived = status?.arrived ?? false;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        if (arrived && status?.enter != null) ...<Widget>[
          Text(
            '${l10n.sessionArrivalCheckedIn} · '
            '${formatSaudiTime12(status!.enter!)}',
            style: SimfTokens.labelBeigeSemiboldSm,
          ),
          const SizedBox(height: SimfTokens.space2),
        ],
        SizedBox(
          width: double.infinity,
          child: arrived
              ? OutlinedButton(
                  onPressed: _busy ? null : () => unawaited(_depart()),
                  style: OutlinedButton.styleFrom(
                    minimumSize:
                        const Size.fromHeight(SimfTokens.controlHeight),
                    side: const BorderSide(color: SimfTokens.accent),
                    shape: const RoundedRectangleBorder(
                      borderRadius: SimfTokens.borderRadiusSmall,
                    ),
                  ),
                  child: Text(
                    l10n.sessionArrivalDepart,
                    style: SimfTokens.labelWhiteBoldLg,
                  ),
                )
              : FilledButton.icon(
                  onPressed: _busy ? null : () => unawaited(_arrive()),
                  style: FilledButton.styleFrom(
                    minimumSize:
                        const Size.fromHeight(SimfTokens.controlHeight),
                    backgroundColor: SimfTokens.accent,
                    foregroundColor: SimfTokens.surface,
                    shape: const RoundedRectangleBorder(
                      borderRadius: SimfTokens.borderRadiusSmall,
                    ),
                  ),
                  icon: const Icon(
                    Icons.place_outlined,
                    size: 24,
                    color: SimfTokens.surface,
                  ),
                  label: Text(
                    l10n.sessionArrivalAction,
                    style: SimfTokens.labelWhiteBoldLg,
                  ),
                ),
        ),
      ],
    );
  }

  Future<void> _arrive() async {
    final reading = await ref.read(deviceLocationProvider).read();
    final position = reading.position;
    if (reading.outcome != DeviceLocationOutcome.granted || position == null) {
      _tell(widget.l10n.sessionArrivalLocationDenied);
      return;
    }

    setState(() => _busy = true);
    try {
      final status = await ref
          .read(hallAttendanceRepositoryProvider)
          .recordArrival(
            widget.sessionId,
            lat: position.lat,
            lon: position.lon,
          );
      if (!mounted) {
        return;
      }
      setState(() => _status = status);
      _tell(widget.l10n.sessionArrivalCheckedIn);
    } on ApiFailure catch (failure) {
      _tell(_messageFor(failure));
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  Future<void> _depart() async {
    setState(() => _busy = true);
    try {
      final status = await ref
          .read(hallAttendanceRepositoryProvider)
          .recordDeparture(widget.sessionId);
      if (!mounted) {
        return;
      }
      setState(() => _status = status);
      _tell(widget.l10n.sessionArrivalDeparted);
    } on ApiFailure catch (failure) {
      _tell(_messageFor(failure));
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  /// The two outcomes that are NOT failures get their own plain wording: a hall
  /// with no boundary yet, and standing outside the radius. Everything else
  /// shows the server's own bilingual message (the envelope already carries the
  /// caller's language).
  String _messageFor(ApiFailure failure) {
    if (failure.code == HallAttendanceRepository.hallGeofenceNotConfigured) {
      return widget.l10n.sessionArrivalNoBoundary;
    }
    if (failure.code == HallAttendanceRepository.notAtVenue) {
      return widget.l10n.sessionArrivalOutsideHall;
    }
    return failure.message;
  }

  void _tell(String message) {
    if (!mounted) {
      return;
    }
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }
}
