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
  SessionDetail? _detail;
  MySeat? _mySeat;

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
      // The my-seat card is approved-account only; a guest never calls the seat
      // endpoint, and a pending account's 403 simply leaves the card hidden.
      final seat = ref.read(authControllerProvider) is AuthStateSignedIn
          ? await _safeMySeat(repo)
          : null;
      if (!mounted) {
        return;
      }
      setState(() {
        _detail = detail;
        _mySeat = seat;
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

  Future<MySeat?> _safeMySeat(SessionDetailRepository repo) async {
    try {
      return await repo.getMySeat(widget.sessionId);
    } on ApiFailure {
      // 401 (no token) / 403 (not approved) / transport → no card (L-3).
      return null;
    }
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
    return _Content(
      detail: _detail!,
      mySeat: _mySeat,
      l10n: l10n,
      onAddToCalendar: () => unawaited(_addToCalendar(_detail!, l10n)),
      onRemind: () => _remind(l10n),
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
    required this.mySeat,
    required this.l10n,
    required this.onAddToCalendar,
    required this.onRemind,
    required this.onViewSeat,
    required this.onSpeaker,
  });

  final SessionDetail detail;
  final MySeat? mySeat;
  final AppL10n l10n;
  final VoidCallback onAddToCalendar;
  final VoidCallback onRemind;
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
        _HeaderCard(detail: detail, isArabic: isArabic),
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
              onTap: () => onSpeaker(speaker),
            ),
            const SizedBox(height: SimfTokens.space4),
          ],
        ],
        if (mySeat != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space1),
          _SectionHeading(l10n.mySeatHeading),
          const SizedBox(height: SimfTokens.space4),
          _SeatCard(seat: mySeat!, l10n: l10n, onView: onViewSeat),
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

/// The session header card (frame 889:2716): a navy box holding the ordinal +
/// gold index badge, the clock/calendar meta line, the title, and the hall +
/// category tag pills — all right-aligned for RTL.
class _HeaderCard extends StatelessWidget {
  const _HeaderCard({required this.detail, required this.isArabic});

  final SessionDetail detail;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final hall = detail.localizedHall(isArabic);
    final category = detail.localizedCategory(isArabic);
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
          Wrap(
            alignment: WrapAlignment.start,
            spacing: SimfTokens.space2,
            runSpacing: SimfTokens.space2,
            children: <Widget>[
              _TagPill(
                label: hall,
                accented: true,
                icon: Icons.place_outlined,
              ),
              if (category != null) _TagPill(label: category, accented: false),
            ],
          ),
        ],
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

/// A hall / category tag pill (frame 889:2708/889:2709): a navy bordered chip.
/// The hall (location) pill is gold-accented with a place icon; the category
/// pill uses the beige hairline border with white text.
class _TagPill extends StatelessWidget {
  const _TagPill({required this.label, required this.accented, this.icon});

  final String label;
  final bool accented;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final fg = accented ? SimfTokens.accent : Colors.white;
    return Container(
      height: 34,
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: accented ? SimfTokens.accent : SimfTokens.beigeBorder,
          width: accented ? SimfTokens.hairlineBold : SimfTokens.hairline,
        ),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Text(
            label,
            style: TextStyle(
              color: fg,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w600,
            ),
          ),
          if (icon != null) ...<Widget>[
            const SizedBox(width: SimfTokens.space2),
            Icon(icon, size: 14, color: fg),
          ],
        ],
      ),
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

/// One speaker card (frame 889:2722/889:2737/889:2747): a navy box with a
/// beige hairline; a gold-tinted icon box on the inline-start (physical right)
/// — an anchor for a speaker, a star for the host — with the name (white 16px)
/// over the rank (beige 12px) beside it. Tapping opens the speaker profile.
class _SpeakerCard extends StatelessWidget {
  const _SpeakerCard({
    required this.speaker,
    required this.isArabic,
    required this.hostLabel,
    required this.onTap,
  });

  final SessionSpeaker speaker;
  final bool isArabic;
  final String hostLabel;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final name = speaker.localizedName(isArabic);
    final country = speaker.localizedCountry(isArabic);
    final isHost = speaker.role == SessionSpeakerRole.host;
    final subParts = <String>[
      if (speaker.title != null && speaker.title!.trim().isNotEmpty)
        speaker.title!.trim(),
      if (country != null) country,
      if (isHost) hostLabel,
    ];

    return KsaCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Row(
          children: <Widget>[
            _RoleBox(isHost: isHost),
            const SizedBox(width: SimfTokens.space4),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    name,
                    style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w600,
                      fontSize: SimfTokens.textLg,
                    ),
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

/// The gold-tinted role box on a speaker card (frame 889:2731/889:2757): a 44×44
/// rounded square with a gold border and a gold glyph — an anchor for a speaker,
/// a star for the host.
class _RoleBox extends StatelessWidget {
  const _RoleBox({required this.isHost});

  final bool isHost;

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
      child: Icon(
        isHost ? Icons.star_outline : Icons.anchor,
        size: 20,
        color: SimfTokens.accent,
      ),
    );
  }
}

/// The my-seat card (frame 889:2761): a navy box with the beige hairline,
/// holding a gold filled marker box on the inline-start (physical right), the
/// row · seat line over the badge hint beside it, and a forward chevron on the
/// inline-end (left) opening the seat map (18). There is no column axis
/// (Page_017 L-3.1).
class _SeatCard extends StatelessWidget {
  const _SeatCard({
    required this.seat,
    required this.l10n,
    required this.onView,
  });

  final MySeat seat;
  final AppL10n l10n;
  final VoidCallback onView;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      onTap: onView,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Row(
          children: <Widget>[
            // Frame 894:2779 — a gold filled marker box at the inline start
            // (physical right); the "View" affordance for the seat map.
            // Labelled for screen readers.
            Semantics(
              button: true,
              label: l10n.seatViewLink,
              child: const _SeatMarker(),
            ),
            const SizedBox(width: SimfTokens.space4),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    l10n.seatLocation(seat.rowLabel, seat.seatNumber),
                    style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w600,
                      fontSize: SimfTokens.textLg,
                    ),
                  ),
                  const SizedBox(height: SimfTokens.space2),
                  Text(
                    l10n.seatBadgeHint,
                    style: const TextStyle(
                      color: SimfTokens.beigeBorder,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            // Frame 889:2762 — a forward chevron at the inline end (physical
            // left under RTL).
            const Icon(
              Icons.chevron_left,
              size: 20,
              color: SimfTokens.beigeBorder,
            ),
          ],
        ),
      ),
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
