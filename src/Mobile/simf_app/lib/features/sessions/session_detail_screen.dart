import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_confirm_dialog.dart';
import 'package:simf_app/app/widgets/simf_info_dialog.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/moderation/data/moderation_repository.dart';
import 'package:simf_app/features/sessions/data/seat_map_repository.dart';
import 'package:simf_app/features/sessions/data/session_calendar.dart';
import 'package:simf_app/features/sessions/data/session_detail_eligibility.dart';
import 'package:simf_app/features/sessions/data/session_detail_repository.dart';
import 'package:simf_app/features/sessions/data/session_lifecycle.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/widgets/session_arrival_action.dart';
import 'package:simf_app/features/sessions/widgets/session_detail_body.dart';
import 'package:simf_app/features/sessions/widgets/session_detail_header.dart';
import 'package:simf_app/features/sessions/widgets/session_detail_states.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Session detail — تفاصيل الجلسة · route: `RouteNames.sessionDetail` ·
/// Figma 889:2450
///
/// Contract: the **Reminder** CTA is a placeholder — reminders are deferred to
/// the notifications platform pass (D-300), so it only toasts.
///
/// Contract (owner 2026-07-22): this screen deliberately does NOT open the rate
/// form when you leave an ended session — viewing a session is not attending
/// it. The rate prompt comes only from watching the live stream
/// (`live_broadcast_screen`) or from the attendance-gated rate notification.
class SessionDetailScreen extends ConsumerStatefulWidget {
  const SessionDetailScreen({required this.sessionId, super.key});

  final String sessionId;

  @override
  ConsumerState<SessionDetailScreen> createState() =>
      _SessionDetailScreenState();
}

class _SessionDetailScreenState extends ConsumerState<SessionDetailScreen> {
  bool _busy = false;

  /// The current load, watched in `build` and read by the actions.
  AsyncValue<SessionDetailView?> get _async =>
      ref.watch(sessionDetailViewProvider(widget.sessionId));

  SessionDetailView? get _view =>
      ref.read(sessionDetailViewProvider(widget.sessionId)).valueOrNull;

  Future<void> _refresh() =>
      refreshAsync(ref, sessionDetailViewProvider(widget.sessionId).future);

  /// DEF-MOD-008 — the role the ROUTER gates on. `appRole` and
  /// `effectiveAppRole` disagree for a signed-in but not-yet-approved account
  /// (D-666 presents it as a guest), and the router reads the effective one —
  /// so a screen that reads the raw role offers affordances the router then
  /// bounces.
  AppRole get _role => roleOf(ref.read(authControllerProvider));

  /// D-485 — join this session. Open-seating → confirm + one-tap join; an
  /// assigned-seat session opens the seat picker (reload on return). A guest /
  /// pending account never reaches here — the join section is hidden for them.
  Future<void> _join(AppL10n l10n) async {
    final map = _view?.seatMap;
    if (map == null || _busy) {
      return;
    }
    if (!map.mode.isOpenSeating) {
      final picked = await context.pushNamed<bool>(
        RouteNames.seatPicker,
        pathParameters: <String, String>{
          RouteParams.sessionId: widget.sessionId,
        },
      );
      if (picked == true && mounted) {
        await _refresh();
      }
      return;
    }
    final confirmed = await _confirm(
      l10n.joinConfirmTitle,
      l10n.joinConfirmBody,
      l10n.joinConfirmAction,
    );
    if (confirmed != true || !mounted) {
      return;
    }
    setState(() => _busy = true);
    final messenger = ScaffoldMessenger.of(context);
    var registered = false;
    try {
      await ref
          .read(seatMapRepositoryProvider)
          .joinOpenSeating(widget.sessionId);
      registered = true;
    } on ApiFailure catch (failure) {
      // Surface the backend's localized reason (already booked / seat selection
      // required / …) instead of a generic "join failed" — the generic toast is
      // why a failed join "looks broken" (matches _cancelReservation below).
      // SEAT_SESSION_FULL keeps its dedicated copy.
      final reason = failure.message.trim();
      final text = failure.code == 'SEAT_SESSION_FULL'
          ? l10n.joinSessionFull
          : (reason.isNotEmpty ? reason : l10n.joinFailed);
      messenger.showSnackBar(SnackBar(content: Text(text)));
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
    // D-750 — case-1 (open-seating) success: a one-button info alert (replaces
    // the old joinPendingToast snackbar) making clear that registering is not a
    // seat reservation and entry is confirmed at check-in.
    if (registered && mounted) {
      await SimfInfoDialog.show(context, title: l10n.joinOpenSuccessBody);
    }
    // The reload below is provider-driven, so leaving while the dialog is
    // up would throw "setState after dispose".
    if (!mounted) {
      return;
    }
    await _refresh();
  }

  /// D-485 — cancel the caller's held reservation (before the session starts).
  Future<void> _cancelReservation(AppL10n l10n) async {
    if (_busy) {
      return;
    }
    final confirmed = await _confirm(
      l10n.cancelBookingConfirmTitle,
      l10n.cancelBookingConfirmBody,
      l10n.cancelBookingCta,
    );
    if (confirmed != true || !mounted) {
      return;
    }
    setState(() => _busy = true);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(seatMapRepositoryProvider).releaseMine(widget.sessionId);
      messenger.showSnackBar(
        SnackBar(content: Text(l10n.bookingCancelledToast)),
      );
    } on ApiFailure catch (failure) {
      // Surface the backend's localized reason (e.g. "cannot cancel after the
      // session has started", "you have no seat to release") instead of a
      // generic failure — the generic toast is the reason cancel "looks
      // broken".
      final reason = failure.message.trim();
      messenger.showSnackBar(
        SnackBar(
          content: Text(reason.isNotEmpty ? reason : l10n.bookingCancelFailed),
        ),
      );
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
    if (!mounted) {
      return;
    }
    await _refresh();
  }

  Future<bool?> _confirm(String title, String body, String action) {
    return SimfConfirmDialog.show(
      context,
      title: title,
      message: body,
      confirmLabel: action,
    );
  }

  Future<void> _addToCalendar(SessionDetail detail, AppL10n l10n) async {
    final messenger = ScaffoldMessenger.of(context);
    final added = await ref
        .read(sessionCalendarProvider)
        .addSession(detail, isArabic: l10n.isArabic);
    if (!mounted) {
      return;
    }
    messenger.showSnackBar(
      SnackBar(content: Text(added ? l10n.calendarAdded : l10n.calendarFailed)),
    );
  }

  void _remind(AppL10n l10n) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(l10n.reminderDeferred)),
    );
  }

  /// رابط الجلسة (Figma 889:2715) — opens the live screen (25) for this
  /// session; only offered when the detail carries a live feed
  /// (`hasLiveStream`).
  void _openLive() => context.pushNamed(
        RouteNames.liveBroadcast,
        queryParameters: <String, String>{
          RouteParams.sessionId: widget.sessionId,
        },
      );

  /// ملخص الجلسة (Figma 889:2715) — opens the AI session summary (34). The
  /// summary screen 404s gracefully until the Committee publishes it.
  void _openSummary() => context.pushNamed(
        RouteNames.aiSummary,
        queryParameters: <String, String>{
          RouteParams.sessionId: widget.sessionId,
        },
      );

  /// اسأل المحاور (Figma 1056:12876) — opens send-question (26). #3 — only
  /// reachable once the user has JOINED the session: the ask card is disabled
  /// (this never fires) until then, so there is no guest/not-joined path here.
  void _askHost() => context.pushNamed(
        RouteNames.sendQuestion,
        queryParameters: <String, String>{
          RouteParams.sessionId: widget.sessionId,
        },
      );

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final async = _async;
    final view = async.valueOrNull;
    // Watched (not read) so the affordances rebuild when the session resolves.
    // DEF-MOD-008 — the ROUTER gates on effectiveAppRole (D-666: an unapproved
    // account presents as guest). Reading the raw `appRole` here showed the
    // moderate action to an unapproved moderator, who was then bounced Home.
    final role = roleOf(ref.watch(authControllerProvider));
    // Moderator (محاور) entry to the Q&A desk (D-405); the grant is
    // per-session, so an empty set while the discovery call is in flight offers
    // no action.
    final moderatedSessionIds =
        ref.watch(myModeratedSessionsProvider).maybeWhen(
              data: (sessions) => sessions.map((s) => s.sessionId).toSet(),
              orElse: () => const <String>{},
            );
    final canModerate =
        canModerateSession(role, moderatedSessionIds, widget.sessionId);
    final canSeat = canAssistSeating(role);
    return SimfPageShell(
      tab: SimfTab.sessions,
      // The frame's chrome is the standard circled back + centred title; the
      // moderator Q&A action (or, for Staff, the seating desk) is kept as a
      // trailing control on the same row.
      header: SessionDetailHeader(
        title: l10n.sessionDetailTitle,
        onBack: () => backOrHome(context),
        actionIcon: canSeat ? Icons.event_seat_outlined : Icons.forum_outlined,
        moderateTooltip: canModerate
            ? l10n.moderatorManageQuestions
            : (canSeat ? l10n.staffSeatingTitle : null),
        onModerate: canModerate
            ? () => _pushWithSessionId(RouteNames.sessionModerate)
            : (canSeat
                ? () => _pushWithSessionId(RouteNames.staffSeating)
                : null),
      ),
      body: SessionDetailStates(
        loading: async.isLoading,
        // Data-null is the 404 (see [sessionDetailViewProvider]); an error is
        // any other failure. Feeding the SAME states widget the same flags is
        // what keeps this conversion pixel-identical.
        notFound: async.hasValue && view == null,
        failed: async.hasError,
        onRefresh: _refresh,
        l10n: l10n,
        onRetry: () =>
            ref.invalidate(sessionDetailViewProvider(widget.sessionId)),
        // Built eagerly, so only reference state that survives every branch:
        // the loaded body reads `view.detail`, which is why it is guarded by
        // the same null check the states widget switches on.
        child: view == null
            ? const SizedBox.shrink()
            // The speaker avatars resolve
            // `{base}/app/assets/SpeakerPhoto/{id}/image` (the D-357
            // SpeakerPhoto asset); the base already includes `/api/v1`.
            : _detailBody(l10n, ref.read(simfDataConfigProvider).baseUrl, view),
      ),
    );
  }

  void _pushWithSessionId(String route) => context.pushNamed(
        route,
        pathParameters: <String, String>{
          RouteParams.sessionId: widget.sessionId,
        },
      );

  /// The scrolling detail itself. The check-in strip goes in as the body's
  /// `header` — the list's FIRST CHILD — rather than being stacked above it:
  /// attendance is about this moment, so it must be readable without scrolling
  /// past the description and speakers, but a widget outside the scrollable
  /// swallows the pull gesture and would break pull-to-refresh at the top of
  /// the page (the standing owner rule that every data page pulls to refresh).
  Widget _detailBody(AppL10n l10n, String baseUrl, SessionDetailView view) {
    final detail = view.detail;
    return SessionDetailBody(
      detail: detail,
      header: showArrivalStatus(detail, _role)
          ? SessionArrivalAction(
              sessionId: widget.sessionId,
              hasEnded: detail.phase(saudiNow()) == SessionPhase.ended,
              l10n: l10n,
            )
          : null,
      seatMap: view.seatMap,
      busy: _busy,
      l10n: l10n,
      baseUrl: baseUrl,
      canAsk: canAskQuestion(_role),
      onAddToCalendar: () => unawaited(_addToCalendar(detail, l10n)),
      onRemind: () => _remind(l10n),
      onSessionLink: _openLive,
      onSessionSummary: _openSummary,
      onAskHost: _askHost,
      onJoin: () => unawaited(_join(l10n)),
      seatMapError: view.seatMapFailed,
      onRetrySeatMap: () => unawaited(_refresh()),
      onCancelReservation: () => unawaited(_cancelReservation(l10n)),
      onViewSeat: () => context.pushNamed(
        RouteNames.mySeat,
        pathParameters: <String, String>{
          RouteParams.sessionId: widget.sessionId,
        },
      ),
      onSpeaker: (speaker) => context.pushNamed(
        RouteNames.speakerProfile,
        pathParameters: <String, String>{RouteParams.speakerId: speaker.id},
      ),
    );
  }
}
