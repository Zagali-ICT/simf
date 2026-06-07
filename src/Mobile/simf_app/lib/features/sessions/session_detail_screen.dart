import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import 'data/session_calendar.dart';
import 'data/session_detail_repository.dart';
import 'data/session_models.dart';

/// Page 017 — تفاصيل الجلسة · Session detail (#17, `/sessions/:sessionId`).
///
/// **Public** (Guest+). On open it fetches the full detail
/// (`GET /app/programme/sessions/{id}`); for a signed-in account it also reads
/// the seat map and shows the **my-seat card** when the caller holds an active
/// reservation (`myCell`, approved-only — guest/pending see no card, L-3). The
/// two CTAs are client-local OS actions: **Add-to-calendar** opens the device
/// calendar pre-filled from the session (E4); the **Reminder** is deferred to the
/// notifications platform pass (D-300). Styled to `Mockup.html` screen 17
/// (`.ag-detail`): a gold index badge + meta + title + tag pills, gold-avatar
/// speaker rows, the brass my-seat card and the two CTAs.
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
    return Scaffold(
      appBar: AppBar(title: Text(l10n.sessionDetailTitle)),
      bottomNavigationBar: const SimfBottomNav(current: SimfTab.sessions),
      body: SafeArea(top: false, child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_notFound) {
      return _EmptyState(
        icon: Icons.event_busy_outlined,
        message: l10n.sessionNotFound,
      );
    }
    if (_error || _detail == null) {
      return _ErrorState(
        message: l10n.sessionDetailError,
        onRetry: () => unawaited(_load()),
      );
    }
    return _content(l10n, _detail!);
  }

  Widget _content(AppL10n l10n, SessionDetail detail) {
    final isArabic = l10n.isArabic;
    final description = detail.localizedDescription(isArabic);
    final category = detail.localizedCategory(isArabic);

    return ListView(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space4,
        SimfTokens.space4,
        SimfTokens.space6,
      ),
      children: <Widget>[
        _Header(detail: detail, isArabic: isArabic),
        const SizedBox(height: SimfTokens.space3),
        _TagsRow(hall: detail.localizedHall(isArabic), category: category),
        if (description != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          _SectionHeading(l10n.descriptionHeading),
          const SizedBox(height: SimfTokens.space2),
          Text(
            description,
            style: const TextStyle(
              color: SimfTokens.txtSecondary,
              height: 1.7,
              fontSize: SimfTokens.textSm,
            ),
          ),
        ],
        if (detail.speakers.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          _SectionHeading(l10n.speakersHeading),
          const SizedBox(height: SimfTokens.space2),
          for (final speaker in detail.speakers)
            _SpeakerCard(
              speaker: speaker,
              isArabic: isArabic,
              hostLabel: l10n.hostLabel,
              onTap: () => context.pushNamed(
                RouteNames.speakerProfile,
                pathParameters: <String, String>{'speakerId': speaker.id},
              ),
            ),
        ],
        if (_mySeat != null) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          _SectionHeading(l10n.mySeatHeading),
          const SizedBox(height: SimfTokens.space2),
          _SeatCard(
            seat: _mySeat!,
            l10n: l10n,
            onView: () => context.pushNamed(
              RouteNames.mySeat,
              pathParameters: <String, String>{'sessionId': widget.sessionId},
            ),
          ),
        ],
        const SizedBox(height: SimfTokens.space6),
        _CtaRow(
          l10n: l10n,
          onAddToCalendar: () => unawaited(_addToCalendar(detail, l10n)),
          onRemind: () => _remind(l10n),
        ),
      ],
    );
  }
}

/// The header band (mockup `.ag-d-head`): a gold index badge · meta line · title.
class _Header extends StatelessWidget {
  const _Header({required this.detail, required this.isArabic});

  final SessionDetail detail;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        if (detail.code.isNotEmpty)
          Container(
            padding: const EdgeInsets.symmetric(
              horizontal: SimfTokens.space2,
              vertical: SimfTokens.space1,
            ),
            decoration: BoxDecoration(
              color: SimfTokens.accent.withValues(alpha: 0.10),
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              border: Border.all(
                color: SimfTokens.accent.withValues(alpha: 0.30),
              ),
            ),
            child: Text(
              detail.code,
              textDirection: TextDirection.ltr,
              style: const TextStyle(
                color: SimfTokens.accent,
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textXs,
              ),
            ),
          ),
        const SizedBox(height: SimfTokens.space2),
        Text(
          _metaLine(detail, isArabic),
          style: const TextStyle(
            color: SimfTokens.txtSecondary,
            fontSize: SimfTokens.textSm,
          ),
        ),
        const SizedBox(height: SimfTokens.space1),
        Text(
          detail.localizedTitle(isArabic),
          style: const TextStyle(
            color: SimfTokens.surface,
            fontWeight: FontWeight.w700,
            fontSize: SimfTokens.textXl,
            height: 1.4,
          ),
        ),
      ],
    );
  }
}

/// The hall + category tag pills (mockup `.ag-d-tags`) — bordered pills, the
/// hall (location) tag accented.
class _TagsRow extends StatelessWidget {
  const _TagsRow({required this.hall, required this.category});

  final String hall;
  final String? category;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: SimfTokens.space2,
      runSpacing: SimfTokens.space2,
      children: <Widget>[
        _Pill(icon: Icons.place_outlined, label: hall, accented: true),
        if (category != null) _Pill(label: category!, accented: false),
      ],
    );
  }
}

class _Pill extends StatelessWidget {
  const _Pill({required this.label, required this.accented, this.icon});

  final IconData? icon;
  final String label;
  final bool accented;

  @override
  Widget build(BuildContext context) {
    final fg = accented ? SimfTokens.accent : SimfTokens.txtSecondary;
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space3,
        vertical: SimfTokens.space1,
      ),
      decoration: BoxDecoration(
        color: accented
            ? SimfTokens.accent.withValues(alpha: 0.06)
            : SimfTokens.surfaceTint,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(
          color: accented
              ? SimfTokens.accent.withValues(alpha: 0.30)
              : SimfTokens.line,
        ),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          if (icon != null) ...<Widget>[
            Icon(icon, size: 13, color: fg),
            const SizedBox(width: SimfTokens.space1),
          ],
          Text(
            label,
            style: TextStyle(color: fg, fontSize: SimfTokens.textXs),
          ),
        ],
      ),
    );
  }
}

/// A small uppercase section heading (mockup `.ag-d-sec h5`).
class _SectionHeading extends StatelessWidget {
  const _SectionHeading(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        color: SimfTokens.txtTertiary,
        fontWeight: FontWeight.w700,
        fontSize: SimfTokens.textXs,
        letterSpacing: 0.8,
      ),
    );
  }
}

/// One speaker row (mockup `.ag-d-spk .sp`): a gold initials avatar, white name
/// and a gold rank/role line. Photos/flags are deferred to SIMF-VID-001.
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
    final subParts = <String>[
      if (speaker.title != null && speaker.title!.trim().isNotEmpty)
        speaker.title!.trim(),
      if (country != null) country,
    ];

    return Card(
      margin: const EdgeInsets.only(bottom: SimfTokens.space2),
      clipBehavior: Clip.antiAlias,
      child: ListTile(
        onTap: onTap,
        leading: CircleAvatar(
          backgroundColor: SimfTokens.accent,
          foregroundColor: SimfTokens.navy,
          child: Text(
            _initials(name),
            style: const TextStyle(fontWeight: FontWeight.w700),
          ),
        ),
        title: Text(
          name,
          style: const TextStyle(
            color: SimfTokens.surface,
            fontWeight: FontWeight.w700,
          ),
        ),
        subtitle: subParts.isEmpty
            ? null
            : Text(
                subParts.join(' · '),
                style: const TextStyle(
                  color: SimfTokens.accent,
                  fontSize: SimfTokens.textXs,
                ),
              ),
        trailing: speaker.role == SessionSpeakerRole.host
            ? Chip(
                label: Text(hostLabel),
                visualDensity: VisualDensity.compact,
                padding: EdgeInsets.zero,
              )
            : const Icon(Icons.chevron_left, color: SimfTokens.txtTertiary),
      ),
    );
  }
}

/// The brass-bordered my-seat card (mockup `.ag-d-seat`) — row + seat (no column
/// axis, L-3.1) + a View link to the seat map (18).
class _SeatCard extends StatelessWidget {
  const _SeatCard({required this.seat, required this.l10n, required this.onView});

  final MySeat seat;
  final AppL10n l10n;
  final VoidCallback onView;

  @override
  Widget build(BuildContext context) {
    return Card(
      color: SimfTokens.accent.withValues(alpha: 0.06),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        side: const BorderSide(color: SimfTokens.accent),
      ),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Row(
          children: <Widget>[
            const Icon(Icons.event_seat_outlined, color: SimfTokens.accent),
            const SizedBox(width: SimfTokens.space3),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    l10n.seatLocation(seat.rowLabel, seat.seatNumber),
                    style: const TextStyle(
                      color: SimfTokens.surface,
                      fontWeight: FontWeight.w700,
                      fontSize: SimfTokens.textMd,
                    ),
                  ),
                  const SizedBox(height: SimfTokens.space1),
                  Text(
                    l10n.seatBadgeHint,
                    style: const TextStyle(
                      color: SimfTokens.txtSecondary,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                ],
              ),
            ),
            TextButton(onPressed: onView, child: Text(l10n.seatViewLink)),
          ],
        ),
      ),
    );
  }
}

/// The two CTAs (mockup `.ag-d-cta`): Add-to-calendar (gold) + Reminder (outlined).
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
    return Row(
      children: <Widget>[
        Expanded(
          child: FilledButton.icon(
            onPressed: onAddToCalendar,
            icon: const Icon(Icons.event_available_outlined),
            label: Text(l10n.addToCalendar),
          ),
        ),
        const SizedBox(width: SimfTokens.space3),
        Expanded(
          child: OutlinedButton.icon(
            onPressed: onRemind,
            icon: const Icon(Icons.notifications_none),
            label: Text(l10n.reminder),
          ),
        ),
      ],
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.icon, required this.message});

  final IconData icon;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(icon, size: 56, color: SimfTokens.txtTertiary),
          const SizedBox(height: SimfTokens.space3),
          Text(message, style: const TextStyle(color: SimfTokens.txtSecondary)),
        ],
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
          ],
        ),
      ),
    );
  }
}

/// First letters of the first two words of [name] — the interim avatar.
String _initials(String name) {
  final words = name.trim().split(RegExp(r'\s+')).where((w) => w.isNotEmpty);
  final letters = words.take(2).map((w) => w.characters.first).join();
  return letters.isEmpty ? '?' : letters.toUpperCase();
}

/// Interim meta line: `weekday · DD month · HH:MM — HH:MM` (device-local).
/// Final locale-aware formatting lands with the designer pass (SIMF-VID-001).
String _metaLine(SessionDetail detail, bool isArabic) {
  final start = detail.startLocal;
  final end = detail.endLocal;
  final weekday = _weekday(start.weekday, isArabic);
  final month = _month(start.month, isArabic);
  final day = start.day.toString().padLeft(2, '0');
  return '$weekday · $day $month · ${_time(start)} — ${_time(end)}';
}

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
