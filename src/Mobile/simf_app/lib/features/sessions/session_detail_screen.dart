import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/router.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../app/widgets/simf_confirm_dialog.dart';
import '../../app/widgets/simf_info_dialog.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../core/utils/saudi_time.dart';
import '../moderation/data/moderation_repository.dart';
import 'data/seat_map_models.dart';
import 'data/seat_map_repository.dart';
import 'data/session_calendar.dart';
import 'data/session_detail_repository.dart';
import 'data/session_lifecycle.dart';
import 'data/session_models.dart';
import 'widgets/session_arrival_action.dart';
import 'widgets/session_detail_body.dart';
import 'widgets/session_detail_header.dart';

/// Page 017 — تفاصيل الجلسة · Session detail (#17, `/sessions/:sessionId`),
/// rebuilt to the KSA-Project Figma frame **889:2450 "Session detail"** on the
/// shared shell.
///
/// **Public** (Guest+). Behaviour contract unchanged: on open it fetches the
/// full detail (`GET /app/programme/sessions/{id}`); for a signed-in account it
/// also reads the seat map and shows the **my-seat card** when the caller holds
/// an active reservation (`myCell`, approved-only — guest/pending see no card,
/// L-3). The two CTAs are client-local OS actions: **Add-to-calendar** opens the
/// device calendar pre-filled from the session (E4); the **Reminder** is
/// deferred to the notifications platform pass (D-300).
///
/// Frame mapping (RTL-primary): a navy session **header card** (gold index
/// badge + ordinal · title · the category tag pill when the session carries a
/// category (PAR-D3) · clock/calendar meta · the ملخص الجلسة / رابط الجلسة
/// actions), the وصف الجلسة description card, the المتحدثون speaker cards
/// (name + rank, the host marked with the gold star + المضيف — PAR-P4a), the gold
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
/// **Rating (owner 2026-07-22):** this screen no longer opens the rate form when
/// you leave an ended session — merely viewing a session is not attending it. The
/// rate prompt now comes only from actually watching the live stream
/// (`live_broadcast_screen`) or from the attendance-gated rate notification after
/// hall check-in/out (plus the day / programme-end prompts). This removes the
/// prompt that used to appear off the sessions list/detail for non-attendees.
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
    // from initState(), and ref.invalidate reaches for the ProviderScope through
    // dependOnInheritedWidgetOfExactType, which Flutter forbids before initState
    // completes — it threw on every mount of this screen. It is also unnecessary:
    // the setState above puts the page into its loading state, which unmounts the
    // check-in strip, and the provider is an autoDispose.family, so it disposes
    // and re-fetches when the strip remounts. Pull-to-refresh therefore refreshes
    // the strip already.
    try {
      final repo = ref.read(sessionDetailRepositoryProvider);
      final detail = await repo.getDetail(widget.sessionId);
      // DEF-MOD-004 — the join / my-seat affordances open the attendee-only
      // routes (#18 my seat, #109 seat picker), so only an attendee's seat map
      // is fetched: a guest / pending account has no join section (L-3), and a
      // Staff / Moderator is not offered one either — the router would bounce
      // them Home the moment they tapped it.
      final canJoin = _canJoin;
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

  /// DEF-MOD-008 — the role the ROUTER gates on. `appRole` and `effectiveAppRole`
  /// disagree for a signed-in but not-yet-approved account (D-666 presents it as
  /// a guest), and the router reads the effective one — so a screen that reads
  /// the raw role offers affordances the router then bounces.
  static AppRole _roleOf(AuthState auth) => auth is AuthStateSignedIn
      ? auth.session.user.effectiveAppRole
      : AppRole.guest;

  AppRole get _role => _roleOf(ref.read(authControllerProvider));

  /// DEF-MOD-004 — join / my-seat are attendee-only routes (#18 and #109 share
  /// the same allowed set), so the UI offers them only to a role that can
  /// actually open them. [routeAllowsRole] is the router's own table (D-519), so
  /// the two can never drift apart.
  bool get _canJoin => routeAllowsRole(RouteNames.mySeat, _role);

  /// DEF-MOD-003 — the اسأل المحاور card opens the attendee-only send-question
  /// route (#26). A GUEST (and a pending account, which presents as one) still
  /// sees the card DISABLED — that is the existing sign-in nudge — but an
  /// operational role the router would bounce is not offered it at all.
  bool get _canAsk {
    final role = _role;
    return role == AppRole.guest ||
        routeAllowsRole(RouteNames.sendQuestion, role);
  }

  /// Whether the hall check-in strip is offered. Three gates, each for its own
  /// reason:
  ///
  /// * It reads the CALLER's own attendance from a bearer-gated endpoint, so it
  ///   follows the same attendee gate as the seat map (D-576/D-577; D-666
  ///   presents a not-yet-approved account as a guest): a guest has no
  ///   attendance to report and would only ever see the failed-read state.
  /// * A session too far in the future has nothing to report yet. But the cut-off
  ///   is NOT "has it started": `HallAttendanceService.RecordGateDoorScanAsync`
  ///   binds a door scan with `s.Start - ArrivalGrace <= now`, where ArrivalGrace
  ///   is 15 minutes, so an attendee scanned in during the queue BEFORE the doors
  ///   nominally open already has a real attendance row. Gating on
  ///   `phase != upcoming` hid the strip for exactly that window — the one where
  ///   people are most likely to have just been scanned. The client mirrors the
  ///   server's grace so the two agree.
  /// * #29 — a workshop's detail is the title + time block only, so it carries
  ///   no attendance section either.
  bool _showArrivalStatus(SessionDetail detail) =>
      _canJoin &&
      detail.type != SessionType.workshop &&
      !saudiNow().isBefore(detail.start.subtract(_arrivalGrace));

  /// Mirrors `HallAttendanceService.ArrivalGrace` (15 minutes). If that server
  /// constant changes, change this with it — they describe the same window from
  /// two sides.
  static const Duration _arrivalGrace = Duration(minutes: 15);

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
        pathParameters: <String, String>{RouteParams.sessionId: widget.sessionId},
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
    // _load() opens with an unguarded setState, so leaving while the dialog is up
    // would throw "setState after dispose".
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
      // generic failure — the generic toast is the reason cancel "looks broken".
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

  /// رابط الجلسة (Figma 889:2715) — opens the live screen (25) for this session;
  /// only offered when the detail carries a live feed (`hasLiveStream`).
  void _openLive() => context.pushNamed(
        RouteNames.liveBroadcast,
        queryParameters: <String, String>{RouteParams.sessionId: widget.sessionId},
      );

  /// ملخص الجلسة (Figma 889:2715) — opens the AI session summary (34). The
  /// summary screen 404s gracefully until the Committee publishes it.
  void _openSummary() => context.pushNamed(
        RouteNames.aiSummary,
        queryParameters: <String, String>{RouteParams.sessionId: widget.sessionId},
      );

  /// اسأل المحاور (Figma 1056:12876) — opens send-question (26). #3 — only
  /// reachable once the user has JOINED the session: the ask card is disabled
  /// (this never fires) until then, so there is no guest/not-joined path here.
  void _askHost() => context.pushNamed(
        RouteNames.sendQuestion,
        queryParameters: <String, String>{RouteParams.sessionId: widget.sessionId},
      );

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    // Watched (not read) so the affordances rebuild when the session resolves.
    // DEF-MOD-008 — the ROUTER gates on effectiveAppRole (D-666: an unapproved
    // account presents as guest). Reading the raw `appRole` here showed the
    // moderate action to an unapproved moderator, who was then bounced Home.
    final role = _roleOf(ref.watch(authControllerProvider));
    // Moderator (محاور) entry to the Q&A desk (D-405). Moderator-EXCLUSIVE
    // (D-519): Staff no longer inherits it (the focused role model dropped the
    // isAtLeast ladder). The server still enforces the per-session
    // SessionModerator grant (403).
    //
    // FR-MOD-001 — the role alone is NOT the gate any more. The grant is
    // per-session, so the icon used to appear on every session in the programme
    // and the missing grant was only discoverable as a 403 after the tap. The
    // action now needs a CONFIRMED grant for this session; while the discovery
    // call is in flight, or if it failed, no action is offered (an icon that
    // 403s is worse than none — the moderator's own home lists their sessions
    // and surfaces the failure there with a retry).
    final moderatedSessionIds = ref.watch(myModeratedSessionsProvider).maybeWhen(
          data: (sessions) =>
              sessions.map((s) => s.sessionId).toSet(),
          orElse: () => const <String>{},
        );
    final canModerate = role == AppRole.moderator &&
        moderatedSessionIds.contains(widget.sessionId);
    // D-771 — Staff entry to the seating desk. Staff and Moderator are disjoint
    // focused roles (D-519), so the two never compete for the header's single
    // trailing slot. UX gate only — the server enforces Seating.Assist (403).
    final canAssistSeating = role == AppRole.staff;
    return SimfPageShell(
      tab: SimfTab.sessions,
      // The frame's chrome is the standard circled back + centred title; the
      // moderator Q&A action (or, for Staff, the seating desk) is kept as a
      // trailing control on the same row.
      header: SessionDetailHeader(
        title: l10n.sessionDetailTitle,
        onBack: () => backOrHome(context),
        actionIcon: canAssistSeating
            ? Icons.event_seat_outlined
            : Icons.forum_outlined,
        moderateTooltip: canModerate
            ? l10n.moderatorManageQuestions
            : (canAssistSeating ? l10n.staffSeatingTitle : null),
        onModerate: canModerate
            ? () => context.pushNamed(
                  RouteNames.sessionModerate,
                  pathParameters: <String, String>{
                    RouteParams.sessionId: widget.sessionId,
                  },
                )
            : (canAssistSeating
                ? () => context.pushNamed(
                      RouteNames.staffSeating,
                      pathParameters: <String, String>{
                        RouteParams.sessionId: widget.sessionId,
                      },
                    )
                : null),
      ),
      body: _buildBody(l10n),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    // The not-found / error states are hosted in an always-scrollable list so a
    // pull-down still fires SimfPullToRefresh (pull to retry) even though they render a
    // short, centred surface.
    if (_notFound) {
      return SimfPullToRefresh(
        onRefresh: _load,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          children: <Widget>[
            SimfEmptyState(
              icon: Icons.event_busy_outlined,
              message: l10n.sessionNotFound,
            ),
          ],
        ),
      );
    }
    if (_error || _detail == null) {
      return SimfPullToRefresh(
        onRefresh: _load,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          children: <Widget>[
            SimfErrorState(
              message: l10n.sessionDetailError,
              retryLabel: l10n.retryLabel,
              onRetry: () => unawaited(_load()),
            ),
          ],
        ),
      );
    }
    // The speaker avatars resolve `{base}/app/assets/SpeakerPhoto/{id}/image`
    // (the D-357 SpeakerPhoto asset); the base already includes `/api/v1`.
    final baseUrl = ref.read(simfDataConfigProvider).baseUrl;
    return SimfPullToRefresh(
      onRefresh: _load,
      child: _detailBody(l10n, baseUrl),
    );
  }

  /// The scrolling detail itself. The check-in strip goes in as the body's
  /// `header` — the list's FIRST CHILD — rather than being stacked above it:
  /// attendance is about this moment, so it must be readable without scrolling
  /// past the description and speakers, but a widget outside the scrollable
  /// swallows the pull gesture and would break pull-to-refresh at the top of the
  /// page (the standing owner rule that every data page pulls to refresh).
  Widget _detailBody(AppL10n l10n, String baseUrl) {
    return SessionDetailBody(
      detail: _detail!,
      header: _showArrivalStatus(_detail!)
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
      canAsk: _canAsk,
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
        pathParameters: <String, String>{RouteParams.sessionId: widget.sessionId},
      ),
      onSpeaker: (speaker) => context.pushNamed(
        RouteNames.speakerProfile,
        pathParameters: <String, String>{RouteParams.speakerId: speaker.id},
      ),
    );
  }
}
