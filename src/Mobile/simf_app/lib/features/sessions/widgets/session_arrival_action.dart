import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../../../core/utils/saudi_time.dart';
import '../data/hall_attendance_repository.dart';

/// FR-305 / FR-506 — the attendee's own **hall check-in status** on the session
/// detail. Read-only: it reports what the door recorded, it never claims an
/// arrival.
///
/// **Owner decision (2026-07-31): the GATE SCAN establishes arrival, not GPS.**
/// The operator's badge scan at the hall door (`GateOperatorService` →
/// `RecordGateDoorScanAsync`) opens / closes the `HallAttendance` row for the
/// session live in that hall, so the whole server chain already exists and the
/// app only reads it back from
/// `GET /app/sessions/{id}/attendance`. That replaced the old
/// "أنا هنا / I'm here" button, which posted a device GPS fix to
/// `POST /app/sessions/{id}/arrival` — and with it the location plugin and the
/// runtime location permission that button needed.
///
/// Three states, kept deliberately distinct:
///
/// * **checked in** — the recorded arrival on the Saudi 12-hour clock, plus the
///   departure line when the door has since scanned the attendee out.
/// * **not checked in yet** — a calm instruction to present the badge at the
///   hall door. That is a normal state, not an error.
/// * **could not load** — the shared error + retry surface. A failed read is
///   NEVER collapsed into "not checked in": the two mean different things, and
///   only one of them is fixed by trying again.
class SessionArrivalAction extends ConsumerWidget {
  const SessionArrivalAction({
    required this.sessionId,
    required this.hasEnded,
    required this.l10n,
    super.key,
  });

  final String sessionId;

  /// Whether the session is already over. Only the "not checked in" copy depends
  /// on it: telling someone to present a badge at the door of a session that
  /// finished hours ago is an instruction they cannot act on, and an attendee
  /// attends a handful of sessions out of dozens, so that line would otherwise
  /// appear on nearly every past session they simply did not go to.
  final bool hasEnded;

  final AppL10n l10n;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return ref.watch(hallAttendanceStatusProvider(sessionId)).when(
          data: (status) {
            final enter = status.enter;
            if (enter != null) {
              return _checkedIn(enter, status.leave);
            }
            // No attendance on a finished session: say nothing. The absence is
            // not news, and the only copy that fits here would be an instruction
            // that is no longer actionable.
            return hasEnded ? const SizedBox.shrink() : _notCheckedIn();
          },
          error: (_, __) => SimfErrorState(
            message: l10n.sessionArrivalStatusError,
            retryLabel: l10n.retryLabel,
            onRetry: () =>
                ref.invalidate(hallAttendanceStatusProvider(sessionId)),
          ),
          // The state is UNKNOWN while the read is in flight, so nothing is
          // claimed and nothing shifts the page: rendering "not checked in yet"
          // here would be the same collapse the error branch avoids.
          loading: () => const SizedBox.shrink(),
        );
  }

  /// The door has scanned the attendee in: the recorded instant, and the
  /// departure below it once the door has scanned them back out.
  Widget _checkedIn(DateTime enter, DateTime? leave) {
    return _strip(
      icon: Icons.how_to_reg_outlined,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            '${l10n.sessionArrivalCheckedIn} · ${formatSaudiTime12(enter)}',
            style: SimfTokens.labelWhiteSemiboldSm,
          ),
          if (leave != null) ...<Widget>[
            const SizedBox(height: SimfTokens.space1),
            Text(
              '${l10n.sessionArrivalDeparted} · ${formatSaudiTime12(leave)}',
              style: SimfTokens.labelBeigeSm,
            ),
          ],
        ],
      ),
    );
  }

  /// No attendance row yet — tell the attendee where check-in actually happens.
  /// The badge glyph, not a scanner one: the attendee presents the badge, the
  /// operator does the scanning, and the card is not a control.
  Widget _notCheckedIn() {
    return _strip(
      icon: Icons.badge_outlined,
      child: Text(
        l10n.sessionArrivalNotYet,
        style: SimfTokens.labelBeigeSm,
      ),
    );
  }

  /// The shared strip chrome — a gold glyph beside the wrapping status text on
  /// the standard navy card, so both states read as one surface.
  static Widget _strip({required IconData icon, required Widget child}) {
    return SimfCard(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space3),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Icon(icon, size: SimfTokens.sessionArrivalActionSize, color: SimfTokens.accent),
            const SizedBox(width: SimfTokens.space3),
            Expanded(child: child),
          ],
        ),
      ),
    );
  }
}
