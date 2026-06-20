import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../core/country_flag.dart';
import 'data/seat_map_models.dart';
import 'data/seat_map_repository.dart';
import 'data/session_calendar.dart';
import 'data/session_detail_repository.dart';
import 'data/session_models.dart';

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
/// تذكير (outlined) + أضف إلى تقويمي (gold) CTA row.
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
    });
    try {
      final repo = ref.read(sessionDetailRepositoryProvider);
      final detail = await repo.getDetail(widget.sessionId);
      // The seat map (myCell + effective mode) is approved-account only; a guest
      // never calls the seat endpoint, and a pending account's 403 leaves the
      // join section hidden (L-3).
      final seatMap = ref.read(authControllerProvider) is AuthStateSignedIn
          ? await _safeSeatMap()
          : null;
      if (!mounted) {
        return;
      }
      setState(() {
        _detail = detail;
        _seatMap = seatMap;
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
        pathParameters: <String, String>{'sessionId': widget.sessionId},
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
    try {
      await ref
          .read(seatMapRepositoryProvider)
          .joinOpenSeating(widget.sessionId);
      messenger.showSnackBar(SnackBar(content: Text(l10n.joinPendingToast)));
    } on ApiFailure catch (failure) {
      final full = failure.code == 'SEAT_SESSION_FULL';
      messenger.showSnackBar(
        SnackBar(content: Text(full ? l10n.joinSessionFull : l10n.joinFailed)),
      );
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
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
    } on ApiFailure {
      messenger.showSnackBar(
        SnackBar(content: Text(l10n.bookingCancelFailed)),
      );
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
    await _load();
  }

  Future<bool?> _confirm(String title, String body, String action) {
    return showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(title),
        content: Text(body),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: Text(AppL10n.of(dialogContext).cancelLabel),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: Text(action),
          ),
        ],
      ),
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
        queryParameters: <String, String>{'sessionId': widget.sessionId},
      );

  /// ملخص الجلسة (Figma 889:2715) — opens the AI session summary (34). The
  /// summary screen 404s gracefully until the Committee publishes it.
  void _openSummary() => context.pushNamed(
        RouteNames.aiSummary,
        queryParameters: <String, String>{'sessionId': widget.sessionId},
      );

  /// اسأل المحاور (Figma 1056:12876) — opens send-question (26). Auth-gated: a
  /// guest is routed to sign-in by the router's gate, like other login-only
  /// actions.
  void _askHost() => context.pushNamed(
        RouteNames.sendQuestion,
        queryParameters: <String, String>{'sessionId': widget.sessionId},
      );

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final auth = ref.watch(authControllerProvider);
    final role = auth is AuthStateSignedIn
        ? auth.session.user.appRole
        : AppRole.guest;
    // Moderator (محاور) entry to the Q&A desk (D-405). UX gate only — the
    // server still enforces the per-session SessionModerator grant (403).
    final canModerate = role.isAtLeast(AppRole.moderator);
    return KsaPage(
      tab: SimfTab.sessions,
      // The frame's chrome is the standard circled back + centred title; the
      // moderator Q&A action is kept as a trailing control on the same row.
      header: _Header(
        title: l10n.sessionDetailTitle,
        onBack: () => ksaBackOrHome(context),
        moderateTooltip: canModerate ? l10n.moderatorManageQuestions : null,
        onModerate: canModerate
            ? () => context.pushNamed(
                  RouteNames.sessionModerate,
                  pathParameters: <String, String>{
                    'sessionId': widget.sessionId,
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
    if (_notFound) {
      return KsaEmptyState(
        icon: Icons.event_busy_outlined,
        message: l10n.sessionNotFound,
      );
    }
    if (_error || _detail == null) {
      return KsaErrorState(
        message: l10n.sessionDetailError,
        retryLabel: l10n.retryLabel,
        onRetry: () => unawaited(_load()),
      );
    }
    // The speaker avatars resolve `{base}/app/assets/SpeakerPhoto/{id}/image`
    // (the D-357 SpeakerPhoto asset); the base already includes `/api/v1`.
    final baseUrl = ref.read(simfDataConfigProvider).baseUrl;
    return _Content(
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
      onCancelReservation: () => unawaited(_cancelReservation(l10n)),
      onViewSeat: () => context.pushNamed(
        RouteNames.mySeat,
        pathParameters: <String, String>{'sessionId': widget.sessionId},
      ),
      onSpeaker: (speaker) => context.pushNamed(
        RouteNames.speakerProfile,
        pathParameters: <String, String>{'speakerId': speaker.id},
      ),
    );
  }
}

/// The page header row: the circled back chevron (physical left), the centred
/// title, and — for a moderator — the trailing Q&A control. Mirrors the shell's
/// default header chrome but swaps the notifications/drawer controller for the
/// session-specific moderator action (frame 889:2453).
class _Header extends StatelessWidget {
  const _Header({
    required this.title,
    required this.onBack,
    this.moderateTooltip,
    this.onModerate,
  });

  final String title;
  final VoidCallback onBack;
  final String? moderateTooltip;
  final VoidCallback? onModerate;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space3,
        vertical: SimfTokens.space2,
      ),
      child: Row(
        textDirection: TextDirection.ltr,
        children: <Widget>[
          SizedBox(
            width: 40,
            height: 40,
            child: KsaBackButton(onBack: onBack),
          ),
          Expanded(
            child: Text(
              title,
              textAlign: TextAlign.center,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              // Figma 889:2456 — 18px / SemiBold white.
              style: const TextStyle(
                fontSize: SimfTokens.textTitle,
                fontWeight: FontWeight.w600,
                color: Colors.white,
              ),
            ),
          ),
          SizedBox(
            width: 40,
            height: 40,
            child: onModerate == null
                ? null
                : IconButton(
                    tooltip: moderateTooltip,
                    onPressed: onModerate,
                    icon: const Icon(
                      Icons.forum_outlined,
                      color: Colors.white,
                      size: 22,
                    ),
                  ),
          ),
        ],
      ),
    );
  }
}

/// The scrolling body: the header card, description, speakers, my-seat card and
/// the CTA row — all RTL-primary on the navy shell (frame 889:2450).
class _Content extends StatelessWidget {
  const _Content({
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
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space2,
        SimfTokens.space4,
        SimfTokens.space6,
      ),
      children: <Widget>[
        _HeaderCard(
          detail: detail,
          isArabic: isArabic,
          l10n: l10n,
          onSessionLink: onSessionLink,
          onSessionSummary: onSessionSummary,
        ),
        if (description != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          _SectionHeading(l10n.descriptionHeading),
          const SizedBox(height: SimfTokens.space4),
          _DescriptionCard(text: description),
        ],
        if (detail.speakers.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          _SectionHeading(l10n.speakersHeading),
          const SizedBox(height: SimfTokens.space4),
          for (final speaker in detail.speakers) ...<Widget>[
            _SpeakerCard(
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
        // my-seat card, shown to everyone (the send-question route is auth-gated
        // downstream).
        const SizedBox(height: SimfTokens.space5),
        _AskHostCard(label: l10n.askHost, onTap: onAskHost),
        // D-485 — the join section (approved account only): a held reservation
        // shows the booking card + Cancel; otherwise the mode-branched Join CTA.
        if (seatMap?.myCell != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          _SectionHeading(l10n.mySeatHeading),
          const SizedBox(height: SimfTokens.space4),
          _ReservationCard(
            cell: seatMap!.myCell!,
            l10n: l10n,
            busy: busy,
            onCancel: onCancelReservation,
            // An open-seating join has no seat to view on the hall map.
            onView: seatMap!.myCell!.kind == SeatReservationKind.openSeating
                ? null
                : onViewSeat,
          ),
        ] else if (seatMap != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          _SectionHeading(l10n.joinSectionHeading),
          const SizedBox(height: SimfTokens.space4),
          _JoinCard(
            mode: seatMap!.mode,
            busy: busy,
            l10n: l10n,
            onJoin: onJoin,
          ),
        ],
        const SizedBox(height: SimfTokens.space6),
        _CtaRow(
          l10n: l10n,
          onAddToCalendar: onAddToCalendar,
          onRemind: onRemind,
        ),
      ],
    );
  }
}

/// The session header card (frame 889:2716): a navy box holding the title +
/// gold index badge, the clock/calendar meta line, and the رابط الجلسة /
/// ملخص الجلسة action buttons — all right-aligned for RTL.
class _HeaderCard extends StatelessWidget {
  const _HeaderCard({
    required this.detail,
    required this.isArabic,
    required this.l10n,
    required this.onSessionLink,
    required this.onSessionSummary,
  });

  final SessionDetail detail;
  final bool isArabic;
  final AppL10n l10n;
  final VoidCallback onSessionLink;
  final VoidCallback onSessionSummary;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          // Ordinal label + gold index badge (frame 889:2706). The badge shows
          // the session code (e.g. "02"); the ordinal is the localized title's
          // implicit position — we lead with the code on the badge and the
          // session title below, matching the frame's number/name pairing.
          Row(
            children: <Widget>[
              if (detail.code.isNotEmpty) ...<Widget>[
                _IndexBadge(code: detail.code),
                const SizedBox(width: SimfTokens.space2),
              ],
              Expanded(
                child: Text(
                  detail.localizedTitle(isArabic),
                  textAlign: TextAlign.start,
                  // Frame 889:2705 — 16px SemiBold white ordinal line; here it
                  // carries the session title (the real bilingual data).
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w600,
                    fontSize: SimfTokens.textLg,
                    height: 1.4,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space4),
          _MetaRow(detail: detail, isArabic: isArabic),
          const SizedBox(height: SimfTokens.space4),
          // Frame 889:2715 — the two action buttons. رابط الجلسة (live link,
          // beige hairline) only appears when the session has a live feed; it
          // leads (inline-start / physical right under RTL) so ملخص الجلسة
          // (gold hairline) trails, matching the frame.
          Row(
            children: <Widget>[
              if (detail.hasLiveStream) ...<Widget>[
                Expanded(
                  child: _HeaderActionButton(
                    label: l10n.sessionLink,
                    accented: false,
                    onTap: onSessionLink,
                  ),
                ),
                const SizedBox(width: SimfTokens.space2),
              ],
              Expanded(
                child: _HeaderActionButton(
                  label: l10n.sessionSummary,
                  accented: true,
                  onTap: onSessionSummary,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// One header-card action button (frame 889:2708/889:2709): a 34-high navy chip
/// on the 4px radius with a centred 12px SemiBold label. The accented variant
/// (ملخص الجلسة) carries the 0.5px gold hairline + gold text; the plain variant
/// (رابط الجلسة) the 0.2px beige hairline + white text.
class _HeaderActionButton extends StatelessWidget {
  const _HeaderActionButton({
    required this.label,
    required this.accented,
    required this.onTap,
  });

  final String label;
  final bool accented;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final fg = accented ? SimfTokens.accent : Colors.white;
    return Material(
      color: SimfTokens.navyDeep,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Container(
          height: 34,
          alignment: Alignment.center,
          padding: const EdgeInsets.all(SimfTokens.space2),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            border: Border.all(
              color: accented ? SimfTokens.accent : SimfTokens.beigeBorder,
              width: accented ? SimfTokens.hairlineBold : SimfTokens.hairline,
            ),
          ),
          child: Text(
            label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              color: fg,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
      ),
    );
  }
}

/// The gold index badge (frame 889:2604): a 40×40 gold rounded square with the
/// session code in white extrabold, always LTR (e.g. "02"); a longer real code
/// scales down to fit rather than overflowing the badge.
class _IndexBadge extends StatelessWidget {
  const _IndexBadge({required this.code});

  final String code;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 40,
      height: 40,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusLarge + 2),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 4),
        // A two-digit ordinal ("02") shows at full size; a longer real code
        // ("S-001" / "S-TODAY") scales down to fit the 40×40 badge instead of
        // overflowing or wrapping.
        child: FittedBox(
          fit: BoxFit.scaleDown,
          child: Text(
            code,
            textDirection: TextDirection.ltr,
            maxLines: 1,
            softWrap: false,
            style: const TextStyle(
              color: Colors.white,
              fontWeight: FontWeight.w800,
              fontSize: SimfTokens.textLg,
            ),
          ),
        ),
      ),
    );
  }
}

/// The meta line (frame 889:2698): a clock + time range, a separator dot, and a
/// calendar + weekday/day, in beige paragraph text.
class _MetaRow extends StatelessWidget {
  const _MetaRow({required this.detail, required this.isArabic});

  final SessionDetail detail;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final start = detail.startLocal;
    final end = detail.endLocal;
    return Wrap(
      alignment: WrapAlignment.start,
      crossAxisAlignment: WrapCrossAlignment.center,
      spacing: SimfTokens.space3,
      runSpacing: SimfTokens.space1,
      children: <Widget>[
        _MetaItem(
          icon: Icons.schedule_outlined,
          // Times are clock values — keep them LTR so "09:00 — 10:30" reads
          // left-to-right even inside the RTL line.
          label: '${_time(start)} — ${_time(end)}',
          forceLtr: true,
        ),
        const Text(
          '·',
          style: TextStyle(
            color: SimfTokens.beigeBorder,
            fontWeight: FontWeight.w900,
            fontSize: SimfTokens.textLg,
          ),
        ),
        _MetaItem(
          icon: Icons.calendar_today_outlined,
          label: '${_weekday(start.weekday, isArabic)} · '
              '${start.day.toString().padLeft(2, '0')} '
              '${_month(start.month, isArabic)}',
        ),
      ],
    );
  }
}

/// One icon + label pair in the meta line (frame 889:2687/889:2686).
class _MetaItem extends StatelessWidget {
  const _MetaItem({
    required this.icon,
    required this.label,
    this.forceLtr = false,
  });

  final IconData icon;
  final String label;
  final bool forceLtr;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Icon(icon, size: 14, color: SimfTokens.beigeBorder),
        const SizedBox(width: SimfTokens.space2),
        Text(
          label,
          textDirection: forceLtr ? TextDirection.ltr : null,
          style: const TextStyle(
            color: SimfTokens.beigeBorder,
            fontSize: SimfTokens.textSm,
          ),
        ),
      ],
    );
  }
}

/// A section heading (frame 889:2717/889:2720/889:2770): white, 16px Medium,
/// right-aligned for RTL.
class _SectionHeading extends StatelessWidget {
  const _SectionHeading(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        color: Colors.white,
        fontWeight: FontWeight.w500,
        fontSize: SimfTokens.textLg,
      ),
    );
  }
}

/// The description card (frame 889:2719): a navy box with the description in
/// white, 14px, comfortable line height.
class _DescriptionCard extends StatelessWidget {
  const _DescriptionCard({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Text(
        text,
        style: const TextStyle(
          color: Colors.white,
          fontSize: SimfTokens.textMd,
          height: 1.5,
        ),
      ),
    );
  }
}

/// One speaker card (frame 889:2722/889:2737/889:2747): a navy box with a beige
/// hairline; a 40×40 rounded photo on the inline-start (physical right), with
/// the name (white 16px) + the country flag over the rank (beige 12px) beside
/// it. Tapping opens the speaker profile.
class _SpeakerCard extends StatelessWidget {
  const _SpeakerCard({
    required this.speaker,
    required this.isArabic,
    required this.hostLabel,
    required this.baseUrl,
    required this.onTap,
  });

  final SessionSpeaker speaker;
  final bool isArabic;
  final String hostLabel;
  final String baseUrl;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final name = speaker.localizedName(isArabic);
    final flag = countryFlagEmoji(speaker.countryId);
    final isHost = speaker.role == SessionSpeakerRole.host;
    // The country is now carried by the flag (Figma 889:2726), so the second
    // line is the rank + the host marker only.
    final subParts = <String>[
      if (speaker.title != null && speaker.title!.trim().isNotEmpty)
        speaker.title!.trim(),
      if (isHost) hostLabel,
    ];

    return KsaCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Row(
          children: <Widget>[
            _SpeakerAvatar(
              imageUrl: '$baseUrl/app/assets/SpeakerPhoto/${speaker.id}/image',
            ),
            const SizedBox(width: SimfTokens.space4),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  // Name + flag, hugging the inline-start (physical right under
                  // RTL); the name shrinks before the flag so a long name never
                  // pushes the flag off the card.
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: <Widget>[
                      Flexible(
                        child: Text(
                          name,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            color: Colors.white,
                            fontWeight: FontWeight.w600,
                            fontSize: SimfTokens.textLg,
                          ),
                        ),
                      ),
                      if (flag != null) ...<Widget>[
                        const SizedBox(width: SimfTokens.space2),
                        Text(
                          flag,
                          style: const TextStyle(fontSize: SimfTokens.textMd),
                        ),
                      ],
                    ],
                  ),
                  if (subParts.isNotEmpty) ...<Widget>[
                    const SizedBox(height: SimfTokens.space2),
                    Text(
                      subParts.join(' · '),
                      style: const TextStyle(
                        color: SimfTokens.beigeBorder,
                        fontSize: SimfTokens.textSm,
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The speaker's photo on a speaker card (frame 1060:12892): a 40×40 rounded
/// square with a beige hairline. Renders the uploaded SpeakerPhoto asset
/// (D-357), falling back to a navy person glyph while it loads or when the
/// speaker has no photo (the asset route 404s).
class _SpeakerAvatar extends StatelessWidget {
  const _SpeakerAvatar({required this.imageUrl});

  final String imageUrl;

  @override
  Widget build(BuildContext context) {
    const placeholder = ColoredBox(
      color: SimfTokens.navy,
      child: Center(
        child: Icon(Icons.person, size: 20, color: SimfTokens.beigeBorder),
      ),
    );
    return Container(
      width: 40,
      height: 40,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Image.network(
        imageUrl,
        fit: BoxFit.cover,
        gaplessPlayback: true,
        loadingBuilder: (context, child, progress) =>
            progress == null ? child : placeholder,
        errorBuilder: (context, error, stackTrace) => placeholder,
      ),
    );
  }
}

/// The اسأل المحاور card (frame 1056:12876): a full-width navy box with a beige
/// hairline holding a centred user glyph over the 12px SemiBold label. Opens
/// send-question (26) for this session.
class _AskHostCard extends StatelessWidget {
  const _AskHostCard({required this.label, required this.onTap});

  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            const Icon(
              Icons.person_outline,
              size: 24,
              color: Colors.white,
            ),
            const SizedBox(height: SimfTokens.space2),
            Text(
              label,
              style: const TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w600,
                fontSize: SimfTokens.textSm,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// D-485 — the reservation card: the held seat (الصف · مقعد) or "general
/// admission" for an open-seating join, the pending-approval hint, and a Cancel
/// action. A seat-specific booking is tappable to open the seat map (18); an
/// open-seating join has no seat to view, so the card is inert (no chevron).
class _ReservationCard extends StatelessWidget {
  const _ReservationCard({
    required this.cell,
    required this.l10n,
    required this.busy,
    required this.onCancel,
    this.onView,
  });

  final SeatCell cell;
  final AppL10n l10n;
  final bool busy;
  final VoidCallback onCancel;
  final VoidCallback? onView;

  @override
  Widget build(BuildContext context) {
    final isOpen = cell.kind == SeatReservationKind.openSeating;
    final title = isOpen
        ? l10n.generalAdmissionLabel
        : l10n.seatLocation(cell.rowLabel, cell.seatNumber);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        KsaCard(
          onTap: onView,
          child: Padding(
            padding: const EdgeInsets.all(SimfTokens.space2),
            child: Row(
              children: <Widget>[
                Semantics(
                  button: onView != null,
                  label: l10n.seatViewLink,
                  child: const _SeatMarker(),
                ),
                const SizedBox(width: SimfTokens.space4),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        title,
                        style: const TextStyle(
                          color: Colors.white,
                          fontWeight: FontWeight.w600,
                          fontSize: SimfTokens.textLg,
                        ),
                      ),
                      const SizedBox(height: SimfTokens.space2),
                      Text(
                        l10n.reservationPendingHint,
                        style: const TextStyle(
                          color: SimfTokens.beigeBorder,
                          fontSize: SimfTokens.textSm,
                        ),
                      ),
                    ],
                  ),
                ),
                if (onView != null) ...<Widget>[
                  const SizedBox(width: SimfTokens.space2),
                  Icon(
                    Directionality.of(context) == TextDirection.rtl
                        ? Icons.chevron_left
                        : Icons.chevron_right,
                    size: 20,
                    color: SimfTokens.beigeBorder,
                  ),
                ],
              ],
            ),
          ),
        ),
        const SizedBox(height: SimfTokens.space3),
        Align(
          alignment: AlignmentDirectional.centerStart,
          child: TextButton.icon(
            onPressed: busy ? null : onCancel,
            icon: const Icon(Icons.close, size: 18, color: SimfTokens.danger),
            label: Text(
              l10n.cancelBookingCta,
              style: const TextStyle(
                color: SimfTokens.danger,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ),
      ],
    );
  }
}

/// D-485 — the Join CTA, shown to an approved account that holds no reservation.
/// An open-seating session is a one-tap join (confirmed in a dialog upstream); an
/// assigned-seat session opens the seat picker. The label + hint follow the mode.
class _JoinCard extends StatelessWidget {
  const _JoinCard({
    required this.mode,
    required this.busy,
    required this.l10n,
    required this.onJoin,
  });

  final SeatSelectionMode mode;
  final bool busy;
  final AppL10n l10n;
  final VoidCallback onJoin;

  @override
  Widget build(BuildContext context) {
    final open = mode.isOpenSeating;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Text(
          open ? l10n.joinOpenHint : l10n.joinSeatHint,
          style: const TextStyle(
            color: SimfTokens.beigeBorder,
            fontSize: SimfTokens.textSm,
          ),
        ),
        const SizedBox(height: SimfTokens.space3),
        FilledButton.icon(
          onPressed: busy ? null : onJoin,
          style: FilledButton.styleFrom(
            minimumSize: const Size.fromHeight(48),
            backgroundColor: SimfTokens.accent,
            foregroundColor: SimfTokens.surface,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            ),
          ),
          icon: Icon(open ? Icons.how_to_reg : Icons.event_seat, size: 20),
          label: Text(
            open ? l10n.joinOpenCta : l10n.joinSeatCta,
            style: const TextStyle(
              fontWeight: FontWeight.w700,
              fontSize: SimfTokens.textLg,
            ),
          ),
        ),
      ],
    );
  }
}

/// The gold filled marker box on the my-seat card (frame 894:2779): a 44×44
/// gold-bordered tile wrapping a small gold filled square.
class _SeatMarker extends StatelessWidget {
  const _SeatMarker();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 44,
      height: 44,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent.withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(color: SimfTokens.accent),
      ),
      child: Container(
        width: 20,
        height: 20,
        decoration: BoxDecoration(
          color: SimfTokens.accent,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        ),
      ),
    );
  }
}

/// The two CTAs (frame 897:2872): أضف إلى تقويمي (gold filled, calendar icon)
/// fills the inline start, تذكير (outlined, clock icon) trails it. RTL places
/// the gold button on the right and تذكير on the left, as the frame shows.
class _CtaRow extends StatelessWidget {
  const _CtaRow({
    required this.l10n,
    required this.onAddToCalendar,
    required this.onRemind,
  });

  final AppL10n l10n;
  final VoidCallback onAddToCalendar;
  final VoidCallback onRemind;

  @override
  Widget build(BuildContext context) {
    // RTL: the first child is at the inline start (physical right). The frame
    // puts أضف إلى تقويمي (gold) on the right and تذكير (outlined) on the left,
    // so the gold Expanded button leads and the reminder button trails.
    return Row(
      children: <Widget>[
        Expanded(
          child: FilledButton.icon(
            onPressed: onAddToCalendar,
            style: FilledButton.styleFrom(
              minimumSize: const Size.fromHeight(48),
              backgroundColor: SimfTokens.accent,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              ),
            ),
            icon: const Icon(Icons.calendar_today_outlined, size: 24),
            label: Text(
              l10n.addToCalendar,
              style: const TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textLg,
              ),
            ),
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        OutlinedButton.icon(
          onPressed: onRemind,
          style: OutlinedButton.styleFrom(
            // Height 48, width sized to content — this is a non-Expanded child
            // of the Row, so Size.fromHeight (width = infinity) would force an
            // infinite width and crash layout. The calendar button beside it
            // takes the remaining width via Expanded.
            minimumSize: const Size(0, 48),
            padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space6),
            side: const BorderSide(color: SimfTokens.accent),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            ),
          ),
          icon: const Icon(Icons.schedule_outlined, size: 24),
          label: Text(
            l10n.reminder,
            style: const TextStyle(
              color: Colors.white,
              fontWeight: FontWeight.w700,
              fontSize: SimfTokens.textLg,
            ),
          ),
        ),
      ],
    );
  }
}

/// Interim local time format: `HH:MM` (device-local). Final locale-aware
/// formatting lands with the designer pass (SIMF-VID-001).
String _time(DateTime local) {
  final hour = local.hour.toString().padLeft(2, '0');
  final minute = local.minute.toString().padLeft(2, '0');
  return '$hour:$minute';
}

String _weekday(int weekday, bool isArabic) {
  const en = <String>['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  const ar = <String>[
    'الإثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت', 'الأحد',
  ];
  return (isArabic ? ar : en)[weekday - 1];
}

String _month(int month, bool isArabic) {
  const en = <String>[
    'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
    'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
  ];
  const ar = <String>[
    'يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو',
    'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر',
  ];
  return (isArabic ? ar : en)[month - 1];
}
