import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
import 'package:simf_app/features/sessions/data/session_lifecycle.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/widgets/ask_host_card.dart';
import 'package:simf_app/features/sessions/widgets/session_booking_actions.dart';
import 'package:simf_app/features/sessions/widgets/session_header_card.dart';
import 'package:simf_app/features/sessions/widgets/session_reservation_card.dart';
import 'package:simf_app/features/sessions/widgets/session_speaker_card.dart';
import 'package:simf_app/features/sessions/widgets/session_text_sections.dart';

/// The scrolling body: the header card, description, speakers, my-seat card and
/// the CTA row — all RTL-primary on the navy shell (frame 889:2450).
///
/// #29 (owner Q10, 2026-07-30) — a **workshop** is the one exception: its detail
/// is the title + time block ONLY (see [_workshopBody]).
class SessionDetailBody extends StatelessWidget {
  const SessionDetailBody({
    required this.detail,
    required this.seatMap,
    required this.busy,
    required this.l10n,
    required this.baseUrl,
    required this.onAddToCalendar,
    required this.onRemind,
    required this.onSessionLink,
    required this.onSessionSummary,
    required this.onAskHost,
    required this.onJoin,
    required this.canAsk,
    required this.seatMapError,
    required this.onRetrySeatMap,
    required this.onCancelReservation,
    required this.onViewSeat,
    required this.onSpeaker,
    this.header,
    super.key,
  });

  /// Optional strip pinned above the first card — today the gate check-in status.
  /// It is rendered as the ListView's first CHILD rather than stacked above the
  /// list, because a widget outside the scrollable emits no ScrollNotification
  /// and `RefreshIndicator` would therefore ignore a pull that started on it,
  /// breaking pull-to-refresh at the very top of the page.
  final Widget? header;

  final SessionDetail detail;
  // D-485 — the seat map (null for a guest / pending account): drives the join
  // section — the Join CTA when `myCell` is null, the reservation card otherwise.
  final SessionSeatMap? seatMap;
  final bool busy;
  final AppL10n l10n;
  final String baseUrl;
  final VoidCallback onAddToCalendar;
  final VoidCallback onRemind;
  final VoidCallback onSessionLink;
  final VoidCallback onSessionSummary;
  final VoidCallback onAskHost;
  final VoidCallback onJoin;
  // DEF-MOD-003 — whether the اسأل المحاور card is OFFERED at all. The
  // send-question route (#26) is attendee-only, so a Staff / Moderator must not
  // be shown an enabled card that the router then bounces Home. A guest still
  // gets it, disabled (the sign-in nudge).
  final bool canAsk;
  // #18 — an approved attendee whose seat-map fetch failed; when true the join
  // area shows an error+retry (via [onRetrySeatMap]) instead of nothing.
  final bool seatMapError;
  final VoidCallback onRetrySeatMap;
  final VoidCallback onCancelReservation;
  final VoidCallback onViewSeat;
  final void Function(SessionSpeaker speaker) onSpeaker;

  /// The padding the body scrolls inside — shared by the full detail and the
  /// #29 workshop reduction so the two open identically.
  static const EdgeInsets _padding = EdgeInsets.fromLTRB(
    SimfTokens.space4,
    SimfTokens.space2,
    SimfTokens.space4,
    SimfTokens.space6,
  );

  @override
  Widget build(BuildContext context) {
    // #29 (owner Q10, 2026-07-30) — a WORKSHOP shows its title + time ONLY.
    // Everything below this guard is the full session detail.
    if (detail.type == SessionType.workshop) {
      return _workshopBody();
    }

    final isArabic = l10n.isArabic;
    final description = detail.localizedDescription(isArabic);

    // Owner 2026-07-14 — gate the two header actions on the session's phase:
    // the ملخص الجلسة summary exists only once the session has ENDED (a future /
    // live session has none → inactive); رابط الجلسة opens the LIVE feed, so it
    // is active only while the session is live AND carries an online stream.
    final phase = detail.phase(saudiNow());
    final summaryEnabled = phase == SessionPhase.ended;
    final liveEnabled = detail.hasLiveStream && phase == SessionPhase.live;

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: _padding,
      children: <Widget>[
        if (header != null) ...<Widget>[
          header!,
          const SizedBox(height: SimfTokens.space3),
        ],
        SessionHeaderCard(
          detail: detail,
          isArabic: isArabic,
          l10n: l10n,
          onSessionLink: onSessionLink,
          onSessionSummary: onSessionSummary,
          summaryEnabled: summaryEnabled,
          liveEnabled: liveEnabled,
        ),
        if (description != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          SessionSectionHeading(l10n.descriptionHeading),
          const SizedBox(height: SimfTokens.space4),
          SessionDescriptionCard(text: description),
        ],
        if (detail.speakers.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          SessionSectionHeading(l10n.speakersHeading),
          const SizedBox(height: SimfTokens.space4),
          for (final speaker in detail.speakers) ...<Widget>[
            SessionSpeakerCard(
              speaker: speaker,
              isArabic: isArabic,
              hostLabel: l10n.hostLabel,
              baseUrl: baseUrl,
              onTap: () => onSpeaker(speaker),
            ),
            const SizedBox(height: SimfTokens.space4),
          ],
        ],
        // اسأل المحاور (Figma 1056:12876) — sits between the speakers and the
        // my-seat card. The ask is offered until the session ENDS (the backend
        // closes questions at End). #7 (owner): a FUTURE session takes
        // ahead-of-time questions ("before it starts", no booking required). S-4:
        // a LIVE session (now within [start, end]) takes in-hall questions — but
        // only when it has NO broadcast feed; a broadcast session's live ask lives
        // on the live-broadcast screen (check-in gated), so we don't double it.
        // The backend enforces the same window + hall-arrival gate.
        if (canAsk && _showAsk(detail)) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          AskHostCard(
            label: detail.start.isAfter(saudiNow())
                ? l10n.askHostPreSession
                : l10n.liveAskQuestion,
            onTap: onAskHost,
            // A21 (2026-07-27) — REFUTED, deliberately left as-is. The ask is
            // enabled for any approved account (a loaded seat map) and passes
            // NO `disabledHint`, because owner decision #7 / D-733 DROPPED the
            // D-485 join/booking requirement for the pre-start ask ("anyone
            // before start"). `askHostJoinFirst` is therefore unreachable BY
            // DESIGN, not by accident — re-gating on `seatMap?.myCell` would
            // reverse D-733 and breaks its regression test
            // (`#7 — an approved user can ask a FUTURE session without a
            // booking`). A guest / pending account (no seat map) still lands
            // on the disabled branch — the sign-in nudge.
            enabled: seatMap != null,
          ),
        ],
        // D-485 / owner 2026-06-30 — the join section (approved account only).
        // Not booked: the single gold "الانضمام إلى الجلسة" button. Booked: the
        // مقعدي seat card; its cancel is NOT in the card — it sits on its own
        // line as plain white text after the reminder/calendar row (the
        // corrected Figma 889:2450 bottom).
        if (seatMap?.myCell != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          SessionSectionHeading(l10n.mySeatHeading),
          const SizedBox(height: SimfTokens.space4),
          SessionReservationCard(
            cell: seatMap!.myCell!,
            l10n: l10n,
            // An open-seating join has no seat to view on the hall map.
            onView: seatMap!.myCell!.kind == SeatReservationKind.openSeating
                ? null
                : onViewSeat,
          ),
        ] else if (seatMap != null && phase != SessionPhase.ended) ...<Widget>[
          // Owner 2026-07-14 — an ENDED session can't be joined ("open now to
          // join" is a live/upcoming state), so the join CTA drops once it ends.
          const SizedBox(height: SimfTokens.space5),
          SessionJoinButton(
            busy: busy,
            l10n: l10n,
            onJoin: onJoin,
            // D-750 — case-1 (open-seating) reads "register to attend"; case-2
            // (assigned-seat) keeps the default join label.
            label: seatMap!.mode.isOpenSeating
                ? l10n.joinOpenRegisterCta
                : null,
          ),
        ] else if (seatMapError && phase != SessionPhase.ended) ...<Widget>[
          // #18 — an approved attendee whose seat map failed to load gets a
          // retry affordance here instead of a silently-absent Join button.
          const SizedBox(height: SimfTokens.space5),
          SimfErrorState(
            message: l10n.seatMapError,
            retryLabel: l10n.retryLabel,
            onRetry: onRetrySeatMap,
          ),
        ],
        const SizedBox(height: SimfTokens.space6),
        SessionCtaRow(
          l10n: l10n,
          onAddToCalendar: onAddToCalendar,
          onRemind: onRemind,
        ),
        // Booked only — cancel on its own line under reminder/calendar, plain
        // white text (owner 2026-06-30), not the old red ✕ link.
        if (seatMap?.myCell != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space3),
          CancelReservationLink(
            // A13 — the control and the dialog it opens must agree: the dialog is
            // titled "إلغاء الحجز" (cancelBookingConfirmTitle), so the link says
            // "إلغاء الحجز" too, not the bare "إلغاء".
            label: l10n.cancelBookingCta,
            busy: busy,
            onCancel: onCancelReservation,
          ),
        ],
      ],
    );
  }

  /// #29 (owner Q10, 2026-07-30) — the workshop detail: the header card reduced
  /// to its title + time block. No description, no speakers, no اسأل المحاور
  /// card, no seat/join section and no live/summary actions — a workshop is
  /// managed from the existing session admin in the CP and the app publishes
  /// only what it is and when it runs. The list stays scrollable so
  /// pull-to-refresh still fires on the short page.
  Widget _workshopBody() {
    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: _padding,
      children: <Widget>[
        SessionHeaderCard(
          detail: detail,
          isArabic: l10n.isArabic,
          l10n: l10n,
          onSessionLink: onSessionLink,
          onSessionSummary: onSessionSummary,
          summaryEnabled: false,
          liveEnabled: false,
          titleAndTimeOnly: true,
        ),
      ],
    );
  }

  /// S-4 — decides whether the اسأل المحاور card shows. A FUTURE session (before
  /// start) takes ahead-of-time questions; a LIVE session (now within its
  /// [start, end] window) takes in-hall questions but only when it has NO
  /// broadcast feed — a broadcast session's live ask lives on the live-broadcast
  /// screen. After End there is no ask (the backend closes the window).
  static bool _showAsk(SessionDetail detail) {
    final nowUtc = saudiNow();
    if (detail.start.isAfter(nowUtc)) {
      return true;
    }
    if (detail.end.isAfter(nowUtc)) {
      return !detail.hasLiveStream;
    }
    return false;
  }
}
