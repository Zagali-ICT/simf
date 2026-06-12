import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import 'data/session_models.dart';
import 'data/sessions_repository.dart';

/// Page 016 — الأجندة · Sessions agenda (#16, `/sessions`), rebuilt to the
/// KSA Wave-2 frame **215:767 "Calander"** on the shared shell.
///
/// **Public** (Guest+). Behaviour contract unchanged from the mockup build:
/// the whole active programme is fetched once (`GET /app/programme/sessions`)
/// and the pills / day strip / search all filter that cache **client-side**
/// (Page_016 L-1). Frame mapping: the bordered search field, the gold/navy
/// view pills (أجندة الفعالية / الأجندة القادمة), the **white day strip**
/// (data-driven days; the selected day inverts to navy, weekend weekday
/// labels render red; re-tapping the selected day clears back to all days —
/// the frame carries no "all days" pill), then المواعيد rows: a white
/// two-line time chip at the inline start, the gold numbered title + grey
/// description, and a forward chevron. Tapping a row opens the session
/// detail (Page_017).
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
    return KsaPage(
      title: l10n.navAgenda,
      onBack: () =>
          context.canPop() ? context.pop() : context.goNamed(RouteNames.home),
      tab: SimfTab.sessions,
      showSweep: true,
      body: _buildBody(l10n),
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
        Padding(
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space4,
            SimfTokens.space2,
            SimfTokens.space4,
            0,
          ),
          child: _SearchField(
            l10n: l10n,
            onChanged: (value) => setState(() => _query = value),
          ),
        ),
        Padding(
          padding: const EdgeInsets.all(SimfTokens.space4),
          child: _ViewPills(
            l10n: l10n,
            view: _view,
            onChanged: (view) => setState(() => _view = view),
          ),
        ),
        if (days.isNotEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
            child: _DayStrip(
              days: days,
              selected: _selectedDay,
              onChanged: (day) => setState(() => _selectedDay = day),
            ),
          ),
        Padding(
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space4,
            SimfTokens.space5,
            SimfTokens.space4,
            SimfTokens.space3,
          ),
          child: KsaSectionHeader(title: l10n.sessionsScheduleSection),
        ),
        Expanded(
          child: filtered.isEmpty
              ? _EmptyState(message: l10n.sessionsEmpty)
              : ListView.separated(
                  padding: const EdgeInsets.fromLTRB(
                    SimfTokens.space4,
                    0,
                    SimfTokens.space4,
                    SimfTokens.space6,
                  ),
                  itemCount: filtered.length,
                  separatorBuilder: (_, __) =>
                      const SizedBox(height: SimfTokens.space4),
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

/// The bordered search field (frame node 218:722).
class _SearchField extends StatelessWidget {
  const _SearchField({required this.l10n, required this.onChanged});

  final AppL10n l10n;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return TextField(
      onChanged: onChanged,
      textInputAction: TextInputAction.search,
      style: const TextStyle(color: Colors.white, fontSize: SimfTokens.textSm),
      decoration: InputDecoration(
        isDense: true,
        filled: true,
        fillColor: SimfTokens.navyDeep,
        hintText: l10n.sessionsSearchHint,
        hintStyle: const TextStyle(
          color: Colors.white,
          fontSize: SimfTokens.textSm,
        ),
        suffixIcon: const Icon(Icons.search, size: 18, color: Colors.white),
        contentPadding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space3,
          vertical: SimfTokens.space4,
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide:
              const BorderSide(color: SimfTokens.beigeBorder, width: 0.5),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide: const BorderSide(color: SimfTokens.accent),
        ),
      ),
    );
  }
}

/// The two view pills (frame node 218:723): the active pill is solid gold,
/// the inactive one a bordered navy card. Client-side view switch (L-1).
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
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Row(
        children: <Widget>[
          Expanded(
            child: _Pill(
              label: l10n.sessionsViewForum,
              active: view == SessionsView.forum,
              onTap: () => onChanged(SessionsView.forum),
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          Expanded(
            child: _Pill(
              label: l10n.sessionsViewUpcoming,
              active: view == SessionsView.upcoming,
              onTap: () => onChanged(SessionsView.upcoming),
            ),
          ),
        ],
      ),
    );
  }
}

class _Pill extends StatelessWidget {
  const _Pill({required this.label, required this.active, required this.onTap});

  final String label;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: active ? SimfTokens.accent : SimfTokens.navyDeep,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: BorderSide(
          color: active ? SimfTokens.accent : SimfTokens.beigeBorder,
          width: 0.2,
        ),
      ),
      child: InkWell(
        onTap: active ? null : onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: SizedBox(
          height: 48,
          child: Center(
            child: Text(
              label,
              style: const TextStyle(
                color: Colors.white,
                fontSize: SimfTokens.textSm,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// The white day strip (frame node 218:844): one cell per programme day —
/// a short weekday over the day number; the selected day inverts to navy,
/// weekend (Fri/Sat) weekday labels render red. Re-tapping the selected day
/// clears the filter (the frame has no "all days" pill).
class _DayStrip extends StatelessWidget {
  const _DayStrip({
    required this.days,
    required this.selected,
    required this.onChanged,
  });

  final List<DateTime> days;
  final DateTime? selected;
  final ValueChanged<DateTime?> onChanged;

  bool _isSelected(DateTime day) =>
      selected != null &&
      selected!.year == day.year &&
      selected!.month == day.month &&
      selected!.day == day.day;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(
          children: <Widget>[
            for (final day in days)
              Padding(
                padding: const EdgeInsetsDirectional.only(
                  end: SimfTokens.space2,
                ),
                child: _DayCell(
                  day: day,
                  selected: _isSelected(day),
                  onTap: () => onChanged(_isSelected(day) ? null : day),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _DayCell extends StatelessWidget {
  const _DayCell({required this.day, required this.selected, this.onTap});

  final DateTime day;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final weekend =
        day.weekday == DateTime.friday || day.weekday == DateTime.saturday;
    final weekdayColor = selected
        ? Colors.white
        : weekend
            ? SimfTokens.danger
            : SimfTokens.navy;
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: Container(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space2,
          vertical: SimfTokens.space1,
        ),
        decoration: BoxDecoration(
          color: selected ? SimfTokens.navy : Colors.white,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        ),
        child: Column(
          children: <Widget>[
            Text(
              _weekdayEn(day),
              style: TextStyle(
                color: weekdayColor,
                fontSize: SimfTokens.textXs,
                fontWeight: FontWeight.w600,
              ),
            ),
            Text(
              day.day.toString(),
              style: TextStyle(
                color: selected ? Colors.white : SimfTokens.navy,
                fontSize: SimfTokens.textMd,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// One agenda row (frame node 218:845): the white two-line time chip at the
/// inline start, the gold numbered title + grey description, and a forward
/// chevron at the inline end.
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

    return Material(
      color: SimfTokens.navyDeep,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: const BorderSide(color: SimfTokens.beigeBorder, width: 0.2),
      ),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space2,
            vertical: SimfTokens.space4,
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: <Widget>[
              _TimeChip(local: session.startLocal),
              const SizedBox(width: SimfTokens.space4),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text.rich(
                      TextSpan(
                        children: <InlineSpan>[
                          TextSpan(
                            text: index.toString().padLeft(2, '0'),
                            style: const TextStyle(
                              fontSize: SimfTokens.textLg,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                          const TextSpan(text: ' '),
                          TextSpan(text: session.localizedTitle(isArabic)),
                        ],
                      ),
                      style: const TextStyle(
                        color: SimfTokens.accent,
                        fontSize: SimfTokens.textMd,
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                    if (description != null) ...<Widget>[
                      const SizedBox(height: SimfTokens.space2),
                      Text(
                        description,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          color: SimfTokens.beigeBorder,
                          fontSize: SimfTokens.textSm,
                          height: 1.5,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              const Icon(Icons.chevron_left, size: 20, color: Colors.white),
            ],
          ),
        ),
      ),
    );
  }
}

/// The two-line bordered time chip (frame node 221:718): `08:00` over `PM`.
class _TimeChip extends StatelessWidget {
  const _TimeChip({required this.local});

  final DateTime local;

  @override
  Widget build(BuildContext context) {
    final minute = local.minute.toString().padLeft(2, '0');
    final period = local.hour < 12 ? 'AM' : 'PM';
    final hour12 = local.hour % 12 == 0 ? 12 : local.hour % 12;
    return Container(
      width: 52,
      height: 48,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(color: SimfTokens.beigeBorder, width: 0.2),
      ),
      child: Text(
        '$hour12:$minute\n$period',
        textAlign: TextAlign.center,
        textDirection: TextDirection.ltr,
        style: const TextStyle(
          color: Colors.white,
          fontSize: SimfTokens.textSm,
          fontWeight: FontWeight.w700,
          height: 1.4,
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
            Text(
              message,
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white),
            ),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
          ],
        ),
      ),
    );
  }
}

/// A short 3-letter English weekday for the day strip (LTR, as in the frame).
String _weekdayEn(DateTime day) {
  const names = <String>['MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT', 'SUN'];
  return names[day.weekday - 1];
}
