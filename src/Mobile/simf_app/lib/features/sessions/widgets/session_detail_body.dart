import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/seat_map_models.dart';
import '../data/session_models.dart';
import 'ask_host_card.dart';
import 'session_booking_actions.dart';
import 'session_header_card.dart';
import 'session_reservation_card.dart';
import 'session_speaker_card.dart';
import 'session_text_sections.dart';

/// The scrolling body: the header card, description, speakers, my-seat card and
/// the CTA row — all RTL-primary on the navy shell (frame 889:2450).
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
    required this.onCancelReservation,
    required this.onViewSeat,
    required this.onSpeaker,
    super.key,
  });

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
  final VoidCallback onCancelReservation;
  final VoidCallback onViewSeat;
  final void Function(SessionSpeaker speaker) onSpeaker;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    final description = detail.localizedDescription(isArabic);

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space2,
        SimfTokens.space4,
        SimfTokens.space6,
      ),
      children: <Widget>[
        SessionHeaderCard(
          detail: detail,
          isArabic: isArabic,
          l10n: l10n,
          onSessionLink: onSessionLink,
          onSessionSummary: onSessionSummary,
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
        // my-seat card. #7 (owner) — the ask is offered ONLY for a FUTURE session
        // (before it starts): any approved user may ask ahead of time, no booking
        // required. Once the session is live the ask moves to the live-broadcast
        // screen (check-in gated); after it ends there is no ask (the post-session
        // view is a recording, not a live broadcast). The backend enforces the
        // same window + phase-gated venue rule.
        if (detail.startUtc.isAfter(DateTime.now().toUtc())) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          AskHostCard(
            label: l10n.askHostPreSession,
            onTap: onAskHost,
            // Approved accounts may ask ahead; a guest / pending account (no
            // seat map) sees it disabled.
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
        ] else if (seatMap != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          SessionJoinButton(busy: busy, l10n: l10n, onJoin: onJoin),
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
            label: l10n.cancelLabel,
            busy: busy,
            onCancel: onCancelReservation,
          ),
        ],
      ],
    );
  }
}
