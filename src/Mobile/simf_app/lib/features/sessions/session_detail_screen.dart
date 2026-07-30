import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../app/widgets/simf_confirm_dialog.dart';
import '../../app/widgets/simf_info_dialog.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'data/seat_map_models.dart';
import 'data/seat_map_repository.dart';
import 'data/session_calendar.dart';
import 'data/session_detail_repository.dart';
import 'data/session_models.dart';
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
/// badge + ordinal · clock/calendar meta · title · hall + category tag pills),
/// the وصف الجلسة description card, the المتحدثون speaker cards (a gold-tinted
/// anchor box for a speaker / star box for the host, name + rank), the gold
/// مقعدي my-seat card (row · seat + badge hint + a forward chevron), and the
/// تذكير (outlined) + أضف إلى تقويمي (gold) CTA row. The section widgets live
/// in `widgets/` (session_detail_body/header, session_header_card,
/// session_text_sections, session_speaker_card, ask_host_card,
/// session_reservation_card, session_booking_actions).
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
    try {
      final repo = ref.read(sessionDetailRepositoryProvider);
      final detail = await repo.getDetail(widget.sessionId);
      final auth = ref.read(authControllerProvider);
      // The seat map (myCell + effective mode) is approved-account only; a guest
      // never calls the seat endpoint, and a pending account's 403 leaves the
      // join section hidden (L-3).
      final seatMap =
          auth is AuthStateSignedIn ? await _safeSeatMap() : null;
      if (!mounted) {
        return;
      }
      // #18 — the Join affordance is shown to ANY approved signed-in account (the
      // seat map is fetched for all of them, not only visitor/exhibitor), so any
      // of them whose map FAILED deserves the retry. A pending account presents
      // as guest via effectiveAppRole, so it is (correctly) excluded.
      final isApprovedSignedIn = auth is AuthStateSignedIn &&
          auth.session.user.effectiveAppRole != AppRole.guest;
      setState(() {
        _detail = detail;
        _seatMap = seatMap;
        // #18 — a null map for an approved signed-in account means the fetch
        // FAILED (a success always returns a map), so flag it: the body shows a
        // retry instead of silently dropping the Join button.
        _seatMapError = isApprovedSignedIn && seatMap == null;
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
    final auth = ref.watch(authControllerProvider);
    final role = auth is AuthStateSignedIn
        ? auth.session.user.appRole
        : AppRole.guest;
    // Moderator (محاور) entry to the Q&A desk (D-405). Moderator-EXCLUSIVE
    // (D-519): Staff no longer inherits it (the focused role model dropped the
    // isAtLeast ladder). UX gate only — the server still enforces the
    // per-session SessionModerator grant (403).
    final canModerate = role == AppRole.moderator;
    return SimfPageShell(
      tab: SimfTab.sessions,
      // The frame's chrome is the standard circled back + centred title; the
      // moderator Q&A action is kept as a trailing control on the same row.
      header: SessionDetailHeader(
        title: l10n.sessionDetailTitle,
        onBack: () => backOrHome(context),
        moderateTooltip: canModerate ? l10n.moderatorManageQuestions : null,
        onModerate: canModerate
            ? () => context.pushNamed(
                  RouteNames.sessionModerate,
                  pathParameters: <String, String>{
                    RouteParams.sessionId: widget.sessionId,
                  },
                )
            : null,
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
      child: SessionDetailBody(
        detail: _detail!,
        seatMap: _seatMap,
        busy: _busy,
        l10n: l10n,
        baseUrl: baseUrl,
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
      ),
    );
  }
}
