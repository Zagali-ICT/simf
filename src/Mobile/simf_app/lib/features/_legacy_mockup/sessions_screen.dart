import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../sessions/data/session_models.dart';
import '../sessions/data/sessions_repository.dart';

/// LEGACY — the pre-redesign mockup Page 016 sessions list, parked here when
/// the KSA Wave-2 agenda (frame 215:767) replaced it at `/sessions`. Never
/// routed; kept compiling until the owner approves deleting the legacy
/// directory at programme close (§6 freeze rules).
///
/// Page 016 — الجلسات · Sessions (the daily schedule, #16, `/sessions`).
///
/// **Public** (Guest+). On open it fetches the whole active programme once
/// (`GET /app/programme/sessions`, no `day` filter) and caches it in state; the
/// Upcoming/Forum pills, the data-driven day strip and the search box all filter
/// that cache **client-side** (Page_016 L-1) — no per-filter round-trip. Each row
/// matches `Mockup.html` screen 16 (`.ag-item`): gold index · white title ·
/// grey description, with a gold time and a trailing arrow, separated by
/// hairlines. Tapping a row opens the session detail (Page_017).
class SessionsScreen extends ConsumerStatefulWidget {
  const SessionsScreen({super.key});

  @override
  ConsumerState<SessionsScreen> createState() => _SessionsScreenState();
}

class _SessionsScreenState extends ConsumerState<SessionsScreen> {
  bool _loading = true;
  bool _error = false;
  List<SessionListItem> _all = const <SessionListItem>[];

  SessionsView _view = SessionsView.upcoming;
  DateTime? _selectedDay;
  String _query = '';

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
    });
    try {
      final sessions = await ref.read(sessionsRepositoryProvider).getSessions();
      if (!mounted) {
        return;
      }
      setState(() {
        _all = sessions;
        _loading = false;
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = true;
        _loading = false;
      });
    }
  }

  void _openSession(SessionListItem session) {
    context.pushNamed(
      RouteNames.sessionDetail,
      pathParameters: <String, String>{'sessionId': session.id},
    );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.sessionsTitle)),
      bottomNavigationBar: const SimfBottomNav(current: SimfTab.sessions),
      body: SafeArea(top: false, child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error) {
      return _ErrorState(
        message: l10n.sessionsError,
        onRetry: () => unawaited(_load()),
      );
    }

    final isArabic = l10n.isArabic;
    final days = sessionDays(_all);
    final filtered = filterSessions(
      _all,
      view: _view,
      nowUtc: DateTime.now().toUtc(),
      localDay: _selectedDay,
      query: _query,
    );

    return Column(
      children: <Widget>[
        _ViewPills(
          l10n: l10n,
          view: _view,
          onChanged: (view) => setState(() => _view = view),
        ),
        if (days.isNotEmpty)
          _DayStrip(
            l10n: l10n,
            days: days,
            selected: _selectedDay,
            onChanged: (day) => setState(() => _selectedDay = day),
          ),
        _SearchField(
          l10n: l10n,
          onChanged: (value) => setState(() => _query = value),
        ),
        Expanded(
          child: filtered.isEmpty
              ? _EmptyState(message: l10n.sessionsEmpty)
              : ListView.separated(
                  padding: const EdgeInsets.fromLTRB(
                    SimfTokens.space4,
                    SimfTokens.space1,
                    SimfTokens.space4,
                    SimfTokens.space6,
                  ),
                  itemCount: filtered.length,
                  separatorBuilder: (_, __) => const Divider(),
                  itemBuilder: (context, index) => _SessionRow(
                    session: filtered[index],
                    index: index + 1,
                    isArabic: isArabic,
                    onTap: () => _openSession(filtered[index]),
                  ),
                ),
        ),
      ],
    );
  }
}

/// The two filter pills (Upcoming / Forum) — a client-side view switch (L-1).
class _ViewPills extends StatelessWidget {
  const _ViewPills({
    required this.l10n,
    required this.view,
    required this.onChanged,
  });

  final AppL10n l10n;
  final SessionsView view;
  final ValueChanged<SessionsView> onChanged;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space3,
        SimfTokens.space4,
        SimfTokens.space1,
      ),
      child: Row(
        children: <Widget>[
          Expanded(
            child: ChoiceChip(
              label: Center(child: Text(l10n.sessionsViewUpcoming)),
              selected: view == SessionsView.upcoming,
              onSelected: (_) => onChanged(SessionsView.upcoming),
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          Expanded(
            child: ChoiceChip(
              label: Center(child: Text(l10n.sessionsViewForum)),
              selected: view == SessionsView.forum,
              onSelected: (_) => onChanged(SessionsView.forum),
            ),
          ),
        ],
      ),
    );
  }
}

/// The data-driven day strip (mockup `.ag-days`): an "all days" item plus one
/// item per distinct programme day — a short weekday over the day number, the
/// active number in a gold circle.
class _DayStrip extends StatelessWidget {
  const _DayStrip({
    required this.l10n,
    required this.days,
    required this.selected,
    required this.onChanged,
  });

  final AppL10n l10n;
  final List<DateTime> days;
  final DateTime? selected;
  final ValueChanged<DateTime?> onChanged;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 58,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space4,
          vertical: SimfTokens.space1,
        ),
        children: <Widget>[
          _DayItem(
            weekday: l10n.sessionsAllDays,
            number: null,
            selected: selected == null,
            onTap: () => onChanged(null),
          ),
          for (final day in days)
            _DayItem(
              weekday: _weekdayEn(day),
              number: day.day,
              selected: selected != null &&
                  selected!.year == day.year &&
                  selected!.month == day.month &&
                  selected!.day == day.day,
              onTap: () => onChanged(day),
            ),
        ],
      ),
    );
  }
}

class _DayItem extends StatelessWidget {
  const _DayItem({
    required this.weekday,
    required this.number,
    required this.selected,
    required this.onTap,
  });

  final String weekday;
  final int? number;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // "All days" — a single labelled pill (no weekday row).
    if (number == null) {
      return Padding(
        padding: const EdgeInsetsDirectional.only(end: SimfTokens.space4),
        child: Center(
          child: InkWell(
            onTap: onTap,
            borderRadius: BorderRadius.circular(SimfTokens.radius),
            child: Container(
              height: 30,
              padding:
                  const EdgeInsets.symmetric(horizontal: SimfTokens.space3),
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: selected ? SimfTokens.accent : Colors.transparent,
                borderRadius: BorderRadius.circular(SimfTokens.radius),
                border: selected ? null : Border.all(color: SimfTokens.line),
              ),
              child: Text(
                weekday,
                style: TextStyle(
                  color: selected ? SimfTokens.navy : SimfTokens.txtSecondary,
                  fontWeight: FontWeight.w600,
                  fontSize: SimfTokens.textSm,
                ),
              ),
            ),
          ),
        ),
      );
    }
    // A programme day — short weekday over the day number (gold circle if active).
    return Padding(
      padding: const EdgeInsetsDirectional.only(end: SimfTokens.space4),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Text(
              weekday,
              style: const TextStyle(
                color: SimfTokens.txtTertiary,
                fontSize: SimfTokens.textXs,
                fontWeight: FontWeight.w600,
              ),
            ),
            const SizedBox(height: SimfTokens.space1),
            Container(
              width: 30,
              height: 30,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: selected ? SimfTokens.accent : Colors.transparent,
                shape: BoxShape.circle,
              ),
              child: Text(
                number.toString(),
                style: TextStyle(
                  color: selected ? SimfTokens.navy : SimfTokens.txtSecondary,
                  fontWeight: FontWeight.w700,
                  fontSize: SimfTokens.textMd,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The free-text search box (filters title/description/code, both languages).
class _SearchField extends StatelessWidget {
  const _SearchField({required this.l10n, required this.onChanged});

  final AppL10n l10n;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space1,
        SimfTokens.space4,
        SimfTokens.space2,
      ),
      child: TextField(
        onChanged: onChanged,
        textInputAction: TextInputAction.search,
        decoration: InputDecoration(
          isDense: true,
          prefixIcon: const Icon(Icons.search),
          hintText: l10n.sessionsSearchHint,
        ),
      ),
    );
  }
}

/// One session row (mockup `.ag-item`): gold index + white title, grey
/// description, a gold time and a trailing arrow — borderless, hairline-split.
class _SessionRow extends StatelessWidget {
  const _SessionRow({
    required this.session,
    required this.index,
    required this.isArabic,
    required this.onTap,
  });

  final SessionListItem session;
  final int index;
  final bool isArabic;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final description = session.localizedDescription(isArabic);

    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: SimfTokens.space2),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        index.toString().padLeft(2, '0'),
                        textDirection: TextDirection.ltr,
                        style: const TextStyle(
                          color: SimfTokens.accent,
                          fontWeight: FontWeight.w700,
                          fontSize: SimfTokens.textSm,
                        ),
                      ),
                      const SizedBox(width: SimfTokens.space2),
                      Expanded(
                        child: Text(
                          session.localizedTitle(isArabic),
                          style: const TextStyle(
                            color: SimfTokens.surface,
                            fontWeight: FontWeight.w600,
                            fontSize: SimfTokens.textMd,
                          ),
                        ),
                      ),
                    ],
                  ),
                  if (description != null) ...<Widget>[
                    const SizedBox(height: SimfTokens.space1),
                    Text(
                      description,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: SimfTokens.txtSecondary,
                        fontSize: SimfTokens.textSm,
                        height: 1.5,
                      ),
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(width: SimfTokens.space3),
            Padding(
              padding: const EdgeInsets.only(top: 1),
              child: Text(
                _formatTime(session.startLocal),
                textDirection: TextDirection.ltr,
                style: const TextStyle(
                  color: SimfTokens.accent,
                  fontWeight: FontWeight.w600,
                  fontSize: SimfTokens.textSm,
                ),
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            const Icon(
              Icons.chevron_left,
              color: SimfTokens.txtTertiary,
              size: 18,
            ),
          ],
        ),
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(
            Icons.event_busy_outlined,
            size: 56,
            color: SimfTokens.txtTertiary,
          ),
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

/// A short 12-hour `h:mm AM/PM` time for the row (Latin digits, LTR).
String _formatTime(DateTime local) {
  final minute = local.minute.toString().padLeft(2, '0');
  final period = local.hour < 12 ? 'AM' : 'PM';
  final hour12 = local.hour % 12 == 0 ? 12 : local.hour % 12;
  return '$hour12:$minute $period';
}

/// A short 3-letter English weekday for the day strip (LTR, as in the mockup).
String _weekdayEn(DateTime day) {
  const names = <String>['MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT', 'SUN'];
  return names[day.weekday - 1];
}
