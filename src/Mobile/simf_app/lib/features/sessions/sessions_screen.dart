import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import 'data/session_models.dart';
import 'data/sessions_repository.dart';

/// Page 016 — الجلسات · Sessions (the daily schedule, #16, `/sessions`).
///
/// **Public** (Guest+). On open it fetches the whole active programme once
/// (`GET /app/programme/sessions`, no `day` filter) and caches it in state; the
/// Upcoming/Forum pills, the data-driven day strip and the search box all filter
/// that cache **client-side** (Page_016 L-1) — no per-filter round-trip. Each row
/// shows time · index · title · hall · description (the mockup row). Tapping a
/// row opens the session detail (Page_017, `/sessions/:id`). UI is interim; the
/// final visuals come from SIMF-VID-001.
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
      body: SafeArea(child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error) {
      return _ErrorState(message: l10n.sessionsError, onRetry: () => unawaited(_load()));
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
            isArabic: isArabic,
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
                    SimfTokens.space2,
                    SimfTokens.space4,
                    SimfTokens.space6,
                  ),
                  itemCount: filtered.length,
                  separatorBuilder: (_, __) =>
                      const SizedBox(height: SimfTokens.space3),
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
        0,
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

/// The horizontally-scrolling day strip — an "all days" chip plus one chip per
/// distinct programme day (data-driven, Page_016).
class _DayStrip extends StatelessWidget {
  const _DayStrip({
    required this.l10n,
    required this.days,
    required this.selected,
    required this.isArabic,
    required this.onChanged,
  });

  final AppL10n l10n;
  final List<DateTime> days;
  final DateTime? selected;
  final bool isArabic;
  final ValueChanged<DateTime?> onChanged;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 64,
      child: ListView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space4,
          vertical: SimfTokens.space2,
        ),
        children: <Widget>[
          Padding(
            padding: const EdgeInsetsDirectional.only(start: SimfTokens.space2),
            child: ChoiceChip(
              label: Text(l10n.sessionsAllDays),
              selected: selected == null,
              onSelected: (_) => onChanged(null),
            ),
          ),
          for (final day in days)
            Padding(
              padding:
                  const EdgeInsetsDirectional.only(start: SimfTokens.space2),
              child: ChoiceChip(
                label: Text(_dayLabel(day, isArabic)),
                selected: selected != null &&
                    selected!.year == day.year &&
                    selected!.month == day.month &&
                    selected!.day == day.day,
                onSelected: (_) => onChanged(day),
              ),
            ),
        ],
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
        SimfTokens.space2,
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

/// One session row: time column · index chip · title · hall · description, with
/// an optional category ("main session"/type) chip and a trailing chevron.
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
    final hall = session.localizedHall(isArabic);
    final description = session.localizedDescription(isArabic);
    final category = session.localizedCategory(isArabic);

    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space3),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              _TimeColumn(
                time: _formatTime(session.startLocal),
                index: index,
              ),
              const SizedBox(width: SimfTokens.space3),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      session.localizedTitle(isArabic),
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        fontSize: SimfTokens.textMd,
                      ),
                    ),
                    if (hall != null) ...<Widget>[
                      const SizedBox(height: SimfTokens.space1),
                      Text(
                        hall,
                        style: const TextStyle(
                          color: SimfTokens.inkMuted,
                          fontSize: SimfTokens.textSm,
                        ),
                      ),
                    ],
                    if (description != null) ...<Widget>[
                      const SizedBox(height: SimfTokens.space1),
                      Text(
                        description,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          color: SimfTokens.inkMuted,
                          fontSize: SimfTokens.textSm,
                        ),
                      ),
                    ],
                    if (category != null) ...<Widget>[
                      const SizedBox(height: SimfTokens.space2),
                      _CategoryChip(label: category),
                    ],
                  ],
                ),
              ),
              const Icon(Icons.chevron_right, color: SimfTokens.accent),
            ],
          ),
        ),
      ),
    );
  }
}

/// The leading time + index column for a session row.
class _TimeColumn extends StatelessWidget {
  const _TimeColumn({required this.time, required this.index});

  final String time;
  final int index;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: <Widget>[
        Text(
          time,
          style: const TextStyle(
            fontWeight: FontWeight.w700,
            fontSize: SimfTokens.textSm,
          ),
        ),
        const SizedBox(height: SimfTokens.space1),
        Container(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space2,
            vertical: SimfTokens.space1,
          ),
          decoration: BoxDecoration(
            color: SimfTokens.field,
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
          child: Text(
            index.toString().padLeft(2, '0'),
            style: const TextStyle(
              fontWeight: FontWeight.w700,
              fontSize: SimfTokens.textXs,
            ),
          ),
        ),
      ],
    );
  }
}

/// The "main session" / type tag chip (rendered only when a category is set).
class _CategoryChip extends StatelessWidget {
  const _CategoryChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space1,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.field,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        label,
        style: const TextStyle(
          fontSize: SimfTokens.textXs,
          fontWeight: FontWeight.w600,
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
            color: SimfTokens.inkMuted,
          ),
          const SizedBox(height: SimfTokens.space3),
          Text(message, style: const TextStyle(color: SimfTokens.inkMuted)),
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

/// A short 12-hour `h:mm AM/PM` time for the row's leading column. Interim
/// formatting (Latin digits) — the final locale-aware format lands with the
/// designer pass (SIMF-VID-001).
String _formatTime(DateTime local) {
  final minute = local.minute.toString().padLeft(2, '0');
  final period = local.hour < 12 ? 'AM' : 'PM';
  final hour12 = local.hour % 12 == 0 ? 12 : local.hour % 12;
  return '$hour12:$minute $period';
}

/// A `weekday · day` chip label for the day strip (bilingual short weekday).
String _dayLabel(DateTime day, bool isArabic) {
  const weekdaysEn = <String>[
    'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun',
  ];
  const weekdaysAr = <String>[
    'الإثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت', 'الأحد',
  ];
  final names = isArabic ? weekdaysAr : weekdaysEn;
  // DateTime.weekday is 1 (Mon) … 7 (Sun).
  final name = names[day.weekday - 1];
  return '$name ${day.day}';
}
