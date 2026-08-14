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
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/moderation/data/moderation_repository.dart';
import 'package:simf_app/features/sessions/data/seat_map_models.dart';
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

/// Page 017 — تفاصيل الجلسة · Session detail (#17, `/sessions/:sessionId`),
/// rebuilt to the KSA-Project Figma frame **889:2450 "Session detail"** on the
/// shared shell.
///
/// **Public** (Guest+). Behaviour contract unchanged: on open it fetches the
/// full detail (`GET /app/programme/sessions/{id}`); for a signed-in account it
/// also reads the seat map and shows the **my-seat card** when the caller holds
/// an active reservation (`myCell`, approved-only — guest/pending see no card,
/// L-3). The two CTAs are client-local OS actions: **Add-to-calendar** opens
/// the device calendar pre-filled from the session (E4); the **Reminder** is
/// deferred to the notifications platform pass (D-300).
///
/// Frame mapping (RTL-primary): a navy session **header card** (gold index
/// badge + ordinal · title · the category tag pill when the session carries a
/// category (PAR-D3) · clock/calendar meta · the ملخص الجلسة / رابط الجلسة
/// actions), the وصف الجلسة description card, the المتحدثون speaker cards (name
/// + rank, the host marked with the gold star + المضيف — PAR-P4a), the gold
/// مقعدي my-seat card (row · seat + badge hint + a forward chevron), and the
/// تذكير (outlined) + أضف إلى تقويمي (gold) CTA row. The section widgets live
/// in `widgets/` (session_detail_body/header, session_header_card,
/// session_text_sections, session_speaker_card, ask_host_card,
/// session_reservation_card, session_booking_actions).
///
/// **Hall check-in (owner 2026-07-31):** an attendee's arrival at a session is
/// established by the **gate scan** at the hall door, never by device GPS, so
/// the detail carries a read-only [SessionArrivalAction] status strip above the
/// body — it reports what the door recorded (or that nothing was recorded yet)
/// and posts nothing. It replaced the old "أنا هنا" self check-in button.
///
/// **#29 (owner Q10, 2026-07-30) — a WORKSHOP is the exception:** when the
/// detail's `type` is `SessionType.workshop` the body renders the title + time
/// block ONLY (no description, speakers, ask card, seat/join section or
/// live/summary actions). The CP half reuses the existing session admin.
///
/// **Rating (owner 2026-07-22):** this screen no longer opens the rate form
/// when you leave an ended session — merely viewing a session is not attending
/// it. The rate prompt now comes only from actually watching the live stream
/// (`live_broadcast_screen`) or from the attendance-gated rate notification
/// after hall check-in/out (plus the day / programme-end prompts). This removes
/// the prompt that used to appear off the sessions list/detail for
/// non-attendees.
///
/// Route: `RouteNames.sessionDetail`.
/// Data: [authControllerProvider], [myModeratedSessionsProvider],
///       [seatMapRepositoryProvider], [sessionDetailRepositoryProvider],
///       [simfDataConfigProvider].
/// Perf: no list — a single-screen layout.
class SessionDetailScreen extends ConsumerStatefulWidget {
  const SessionDetailScreen({required this.sessionId, super.key});

  final String sessionId;

  @override
  ConsumerState<SessionDetailScreen> createState() =>
      _SessionDetailScreenState();
}

class _SessionDetailScreenState extends ConsumerState<SessionDetailScreen> {
  bool _loading = true;
  bool _error = false;
  bool _notFound = false;
  bool _busy = false;
  SessionDetail? _detail;
  SessionSeatMap? _seatMap;
  // #18 — true when an approved signed-in account's seat-map fetch FAILED (so
  // _seatMap is null because it failed, not because a guest/pending can't join).
  // Drives the join area's error+retry so the Join affordance is never silently
  // absent.
  bool _seatMapError = false;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
      _notFound = false;
      _seatMapError = false;
    });
    // NOTE: do NOT invalidate hallAttendanceStatusProvider here. `_load()` runs
    // from initState(), and ref.invalidate reaches for the ProviderScope
    // through dependOnInheritedWidgetOfExactType, which Flutter forbids before
    // initState completes — it threw on every mount of this screen. It is also
    // unnecessary: the setState above puts the page into its loading state,
    // which unmounts the check-in strip, and the provider is an
    // autoDispose.family, so it disposes and re-fetches when the strip
    // remounts. Pull-to-refresh therefore refreshes the strip already.
    try {
      final repo = ref.read(sessionDetailRepositoryProvider);
      final detail = await repo.getDetail(widget.sessionId);
      // DEF-MOD-004 — the join / my-seat affordances open the attendee-only
      // routes (#18 my seat, #109 seat picker), so only an attendee's seat map
      // is fetched: a guest / pending account has no join section (L-3), and a
      // Staff / Moderator is not offered one either — the router would bounce
      // them Home the moment they tapped it.
      final canJoin = canJoinSession(_role);
      final seatMap = canJoin ? await _safeSeatMap() : null;
      if (!mounted) {
        return;
      }
      setState(() {
        _detail = detail;
        _seatMap = seatMap;
        // #18 — a null map for an attendee means the fetch FAILED (a success
        // always returns a map), so flag it: the body shows a retry instead of
        // silently dropping the Join button.
        _seatMapError = canJoin && seatMap == null;
        _loading = false;
      });
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() {
        _loading = false;
        _notFound = failure.httpStatus == 404;
        _error = failure.httpStatus != 404;
      });
    }
  }

  /// DEF-MOD-008 — the role the ROUTER gates on. `appRole` and
  /// `effectiveAppRole` disagree for a signed-in but not-yet-approved account
  /// (D-666 presents it as a guest), and the router reads the effective one —
  /// so a screen that reads the raw role offers affordances the router then
  /// bounces.
  AppRole get _role => roleOf(ref.read(authControllerProvider));

  Future<SessionSeatMap?> _safeSeatMap() async {
    try {
      return await ref
          .read(seatMapRepositoryProvider)
          .getSeatMap(widget.sessionId);
    } on ApiFailure {
      // 401 (no token) / 403 (not approved) / transport → no join section (L-3).
      return null;
    }
  }

  /// D-485 — join this session. Open-seating → confirm + one-tap join; an
  /// assigned-seat session opens the seat picker (reload on return). A guest /
  /// pending account never reaches here — the join section is hidden for them.
  Future<void> _join(AppL10n l10n) async {
    final map = _seatMap;
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
        await _load();
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
    // _load() opens with an unguarded setState, so leaving while the dialog is
    // up would throw "setState after dispose".
    if (!mounted) {
      return;
    }
    await _load();
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
    await _load();
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
        loading: _loading,
        notFound: _notFound,
        failed: _error || _detail == null,
        onRefresh: _load,
        l10n: l10n,
        onRetry: () => unawaited(_load()),
        // Built eagerly, so only reference state that survives every branch:
        // the loaded body reads `_detail!`, which is why it is guarded by the
        // same `failed` flag the states widget switches on.
        child: _detail == null
            ? const SizedBox.shrink()
            // The speaker avatars resolve
            // `{base}/app/assets/SpeakerPhoto/{id}/image` (the D-357
            // SpeakerPhoto asset); the base already includes `/api/v1`.
            : _detailBody(l10n, ref.read(simfDataConfigProvider).baseUrl),
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
  Widget _detailBody(AppL10n l10n, String baseUrl) {
    return SessionDetailBody(
      detail: _detail!,
      header: showArrivalStatus(_detail!, _role)
          ? SessionArrivalAction(
              sessionId: widget.sessionId,
              hasEnded: _detail!.phase(saudiNow()) == SessionPhase.ended,
              l10n: l10n,
            )
          : null,
      seatMap: _seatMap,
      busy: _busy,
      l10n: l10n,
      baseUrl: baseUrl,
      canAsk: canAskQuestion(_role),
      onAddToCalendar: () => unawaited(_addToCalendar(_detail!, l10n)),
      onRemind: () => _remind(l10n),
      onSessionLink: _openLive,
      onSessionSummary: _openSummary,
      onAskHost: _askHost,
      onJoin: () => unawaited(_join(l10n)),
      seatMapError: _seatMapError,
      onRetrySeatMap: () => unawaited(_load()),
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
