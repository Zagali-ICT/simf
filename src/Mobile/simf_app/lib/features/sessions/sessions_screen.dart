import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../app/widgets/simf_svg_icon.dart';
import 'data/session_models.dart';
import 'data/sessions_repository.dart';

/// Page 016 — برنامج الملتقى · Sessions (#16, `/sessions`), rebuilt to the LIVE
/// KSA frame **883:2308** (D-452).
///
/// **Public** (Guest+). The screen fetches the day-grouped programme once
/// (`GET /app/programme/days`) and filters it client-side. Frame mapping: the
/// bordered search field; the **white day strip** (the programme days — the
/// selected day inverts to navy, weekend weekday labels render red); the
/// selected day's **day title + logo banner** ("تفاصيل اليوم" carries the day's
/// own title); the **type tabs** (الكل / ورش العمل / جلسات / احداث); then the
/// **المواعيد** list — the day's sessions filtered by the active type + the
/// search, the **first one featured** (expanded with the day banner image), the
/// rest collapsed (time chip + numbered title + description + chevron). Tapping a
/// row opens the session detail (Page_017).
class SessionsScreen extends ConsumerStatefulWidget {
  const SessionsScreen({super.key});

  @override
  ConsumerState<SessionsScreen> createState() => _SessionsScreenState();
}

class _SessionsScreenState extends ConsumerState<SessionsScreen> {
  bool _loading = true;
  bool _error = false;
  List<ProgrammeDay> _days = const <ProgrammeDay>[];
  String? _selectedDayId;
  SessionType? _typeFilter; // null = الكل / All
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
      final days = await ref.read(sessionsRepositoryProvider).getDays();
      if (!mounted) {
        return;
      }
      setState(() {
        _days = days;
        _selectedDayId = days.isEmpty ? null : days.first.id;
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
    return SimfPageShell(
      // Frame 883:2314 — the screen header is "برنامج الملتقى" (the bottom-nav
      // tab keeps its own "الأجندة" label).
      title: l10n.sessionsProgrammeTitle,
      onBack: () => backOrHome(context),
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
      // Pull-to-retry: host the centred error state in a scrollable so the
      // pull gesture fires even though the content is short.
      return SimfPullToRefresh(
        onRefresh: _load,
        child: LayoutBuilder(
          builder: (context, constraints) => SingleChildScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            child: ConstrainedBox(
              constraints: BoxConstraints(minHeight: constraints.maxHeight),
              child: SimfErrorState(
                message: l10n.sessionsError,
                retryLabel: l10n.retryLabel,
                onRetry: () => unawaited(_load()),
              ),
            ),
          ),
        ),
      );
    }
    if (_days.isEmpty) {
      // Pull-to-refresh also works on the empty state.
      return SimfPullToRefresh(
        onRefresh: _load,
        child: LayoutBuilder(
          builder: (context, constraints) => SingleChildScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            child: ConstrainedBox(
              constraints: BoxConstraints(minHeight: constraints.maxHeight),
              child: SimfEmptyState(
                icon: Icons.event_busy_outlined,
                message: l10n.sessionsEmpty,
              ),
            ),
          ),
        ),
      );
    }

    final isArabic = l10n.isArabic;
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    final selected = _days.firstWhere(
      (d) => d.id == _selectedDayId,
      orElse: () => _days.first,
    );
    final dayImageUrl = selected.hasImage
        ? '$baseUrl/app/assets/ProgrammeDayImage/${selected.id}/image'
        : null;

    // The selected day's sessions, filtered by the active type tab + the search.
    final needle = _query.trim().toLowerCase();
    final sessions = selected.sessions.where((s) {
      if (_typeFilter != null && s.type != _typeFilter) {
        return false;
      }
      if (needle.isEmpty) {
        return true;
      }
      final haystack = <String?>[
        s.title,
        s.titleArabic,
        s.description,
        s.descriptionArabic,
        s.code,
      ].whereType<String>().join(' ').toLowerCase();
      return haystack.contains(needle);
    }).toList(growable: false);

    return SimfPullToRefresh(
      onRefresh: _load,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space2,
          SimfTokens.space4,
          SimfTokens.space6,
        ),
        children: <Widget>[
          _SearchField(
            l10n: l10n,
            onChanged: (value) => setState(() => _query = value),
          ),
          const SizedBox(height: SimfTokens.space4),
          _DayStrip(
            days: _days,
            selectedId: selected.id,
            onChanged: (id) => setState(() => _selectedDayId = id),
          ),
          const SizedBox(height: SimfTokens.space5),
          // تفاصيل اليوم (883:2327 area) — the selected day's OWN title + its logo
          // banner. The "تفاصيل اليوم" label carries the day title (owner: not a
          // static label — it is the day's title).
          SimfSectionHeader(title: selected.localizedTitle(isArabic)),
          const SizedBox(height: SimfTokens.space3),
          _DayBanner(imageUrl: dayImageUrl),
          const SizedBox(height: SimfTokens.space5),
          // Type tabs (883:2320): الكل / ورش العمل / جلسات / احداث.
          _TypeTabs(
            l10n: l10n,
            active: _typeFilter,
            onChanged: (type) => setState(() => _typeFilter = type),
          ),
          const SizedBox(height: SimfTokens.space5),
          SimfSectionHeader(title: l10n.sessionsScheduleSection),
          const SizedBox(height: SimfTokens.space3),
          if (sessions.isEmpty)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: SimfTokens.space4),
              child: Text(
                l10n.sessionsEmpty,
                style: const TextStyle(color: SimfTokens.beigeBorder),
              ),
            )
          else
            for (final (index, session) in sessions.indexed) ...<Widget>[
              if (index > 0) const SizedBox(height: SimfTokens.space4),
              _SessionRow(
                session: session,
                isArabic: isArabic,
                // The first session of the day is featured — expanded with the
                // day banner image (frame 1310:3232).
                featuredImageUrl: index == 0 ? dayImageUrl : null,
                onTap: () => _openSession(session),
              ),
            ],
        ],
      ),
    );
  }
}

/// The bordered search field (frame node 883:2316).
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
        // Frame 883:2316 — the 18px magnifier hugs the inline-start (physical
        // right under RTL), packed next to the hint (8px from the edge, 8px
        // before the text); the rest of the field is empty. A prefixIcon lands
        // at the inline-start in RTL (a suffixIcon would push it to the left).
        prefixIcon: const Padding(
          padding: EdgeInsetsDirectional.only(
            start: SimfTokens.space2,
            end: SimfTokens.space2,
          ),
          child: SimfSvgIcon(
            'assets/icons/ic_search.svg',
            size: 18,
            color: Colors.white,
          ),
        ),
        prefixIconConstraints: const BoxConstraints(minWidth: 0, minHeight: 0),
        contentPadding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space3,
          vertical: SimfTokens.space3,
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

/// The four type tabs (frame node 883:2320): الكل (all) / ورش العمل (workshops)
/// / جلسات (sessions) / احداث (events). The active tab is solid gold; the rest
/// are bordered navy cards. الكل = no type filter. Client-side filter.
class _TypeTabs extends StatelessWidget {
  const _TypeTabs({
    required this.l10n,
    required this.active,
    required this.onChanged,
  });

  final AppL10n l10n;
  final SessionType? active;
  final ValueChanged<SessionType?> onChanged;

  @override
  Widget build(BuildContext context) {
    // Frame 883:2320 (verified against the render): الكل (All) leads at the
    // inline-start — rightmost in RTL — then احداث · جلسات · ورش العمل. A Row
    // lays children start→end, so All is the first entry.
    final tabs = <(String, SessionType?)>[
      (l10n.sessionTypeAll, null),
      (l10n.sessionTypeEvent, SessionType.event),
      (l10n.sessionTypeSession, SessionType.session),
      (l10n.sessionTypeWorkshop, SessionType.workshop),
    ];
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Row(
        children: <Widget>[
          for (final (i, (label, type)) in tabs.indexed) ...<Widget>[
            if (i > 0) const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: _TypeTab(
                label: label,
                active: type == active,
                onTap: () => onChanged(type),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _TypeTab extends StatelessWidget {
  const _TypeTab({
    required this.label,
    required this.active,
    required this.onTap,
  });

  final String label;
  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
      onTap: active ? null : onTap,
      color: active ? SimfTokens.accent : SimfTokens.navyDeep,
      borderColor: active ? SimfTokens.accent : SimfTokens.beigeBorder,
      child: SizedBox(
        height: 44,
        child: Center(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space1),
            child: Text(
              label,
              textAlign: TextAlign.center,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
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

/// The day banner (frame node 1064:13240): the selected day's logo image under a
/// navy bottom-gradient with the gold anchor badge at the inline-end. A navy
/// anchor-glyph box is the no-logo fall-back. 85 high, full width.
class _DayBanner extends StatelessWidget {
  const _DayBanner({this.imageUrl});

  final String? imageUrl;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 85,
      width: double.infinity,
      child: ClipRRect(
        borderRadius:
            const BorderRadius.all(Radius.circular(SimfTokens.radius)),
        child: Stack(
          fit: StackFit.expand,
          children: <Widget>[
            if (imageUrl != null)
              Image.network(
                imageUrl!,
                fit: BoxFit.cover,
                gaplessPlayback: true,
                loadingBuilder: (context, child, progress) => progress == null
                    ? child
                    : const ColoredBox(color: SimfTokens.navy),
                errorBuilder: (context, error, stackTrace) =>
                    const _DayBannerFallback(),
              )
            else
              const _DayBannerFallback(),
            // The frame's bottom gradient (transparent → navy #001030 @ 80%).
            const DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.center,
                  end: Alignment.bottomCenter,
                  colors: <Color>[Colors.transparent, SimfTokens.bannerScrim],
                ),
              ),
            ),
            // The gold anchor badge (frame 1064:13249) — inline-start (physical
            // right under RTL), matching the frame.
            PositionedDirectional(
              top: SimfTokens.space2,
              start: SimfTokens.space2,
              child: Container(
                width: 32,
                height: 32,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: SimfTokens.accent,
                  borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                ),
                child: const Icon(
                  Icons.anchor,
                  size: 16,
                  color: SimfTokens.navy,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The no-logo / failed-fetch day-banner fall-back: a navy box with the anchor
/// glyph (the designed empty state until a day logo is uploaded).
class _DayBannerFallback extends StatelessWidget {
  const _DayBannerFallback();

  @override
  Widget build(BuildContext context) => const ColoredBox(
        color: SimfTokens.navy,
        child: Center(
          child: Icon(Icons.image_outlined, size: 28, color: SimfTokens.beigeBorder),
        ),
      );
}

/// The agenda day strip (frame node 883:2327, restyled #4): a **grey** calendar
/// band spanning the **full** event date range — every day from the first to the
/// last programme day, not only the days that carry sessions. Each cell shows the
/// weekday over the centred day number. A day **with** sessions is "active"
/// (white); the **selected** day is black; an empty in-between day is muted grey
/// and not selectable. The strip fills the width (cells distributed), falling
/// back to a horizontal scroll when the range is too long to fit.
class _DayStrip extends StatelessWidget {
  const _DayStrip({
    required this.days,
    required this.selectedId,
    required this.onChanged,
  });

  final List<ProgrammeDay> days;
  final String selectedId;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    final entries = _calendarRange(days);
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space2),
      decoration: const BoxDecoration(
        // Frame 883:2327 — the day strip is a WHITE band (the old grey
        // calendarBand was the superseded design).
        color: SimfTokens.surface,
        borderRadius:
            BorderRadius.all(Radius.circular(SimfTokens.radiusSmall)),
      ),
      child: LayoutBuilder(
        builder: (context, constraints) {
          const double gap = SimfTokens.space1;
          const double minCell = 44.0;
          final width = entries.length * minCell + (entries.length - 1) * gap;
          // Guard the unbounded-width case (e.g. if ever placed outside a
          // width-bounded parent) so the Expanded row can't throw.
          final fits = constraints.maxWidth.isFinite && width <= constraints.maxWidth;
          final row = Row(
            mainAxisSize: fits ? MainAxisSize.max : MainAxisSize.min,
            children: <Widget>[
              for (var i = 0; i < entries.length; i++) ...<Widget>[
                if (i > 0) const SizedBox(width: gap),
                if (fits)
                  Expanded(child: _cell(entries[i]))
                else
                  SizedBox(width: minCell, child: _cell(entries[i])),
              ],
            ],
          );
          return fits
              ? row
              : SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: row,
                );
        },
      ),
    );
  }

  Widget _cell(_CalendarDay e) => _DayCell(
        date: e.date,
        hasSessions: e.programmeDay != null,
        selected: e.programmeDay?.id == selectedId,
        onTap:
            e.programmeDay == null ? null : () => onChanged(e.programmeDay!.id),
      );

  /// The contiguous date range from the programme days: span the first to the
  /// last day (inclusive), mapping each date to its [ProgrammeDay] when one
  /// exists (an "active" day) or null (an empty in-between day).
  static List<_CalendarDay> _calendarRange(List<ProgrammeDay> days) {
    if (days.isEmpty) {
      return const <_CalendarDay>[];
    }
    final byDate = <DateTime, ProgrammeDay>{};
    DateTime? first;
    DateTime? last;
    for (final d in days) {
      final date = DateTime(d.date.year, d.date.month, d.date.day);
      byDate[date] = d;
      if (first == null || date.isBefore(first)) {
        first = date;
      }
      if (last == null || date.isAfter(last)) {
        last = date;
      }
    }
    final out = <_CalendarDay>[];
    var cur = first!;
    while (!cur.isAfter(last!)) {
      out.add(_CalendarDay(cur, byDate[cur]));
      cur = DateTime(cur.year, cur.month, cur.day + 1);
    }
    return out;
  }
}

/// One date in the agenda calendar strip: [programmeDay] is null for an empty
/// in-between day (no sessions), set for a day that carries sessions.
class _CalendarDay {
  const _CalendarDay(this.date, this.programmeDay);

  final DateTime date;
  final ProgrammeDay? programmeDay;
}

class _DayCell extends StatelessWidget {
  const _DayCell({
    required this.date,
    required this.hasSessions,
    required this.selected,
    required this.onTap,
  });

  final DateTime date;
  final bool hasSessions;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    // Frame 883:2327 — on the white band: the selected day is a navy pill with
    // white text; a day with sessions shows navy text (no fill); an empty
    // in-between day is muted grey (#C2C2C2). Weekend weekday labels (SAT/SUN)
    // render red on non-selected days.
    final bool isWeekend =
        date.weekday == DateTime.saturday || date.weekday == DateTime.sunday;
    final Color fill;
    final Color numberColor;
    final Color weekdayColor;
    if (selected) {
      fill = SimfTokens.navy;
      numberColor = Colors.white;
      weekdayColor = Colors.white;
    } else if (hasSessions) {
      fill = Colors.transparent;
      numberColor = SimfTokens.navy;
      weekdayColor = isWeekend ? SimfTokens.danger : SimfTokens.navy;
    } else {
      fill = Colors.transparent;
      numberColor = SimfTokens.dayInactive;
      weekdayColor = isWeekend ? SimfTokens.danger : SimfTokens.dayInactive;
    }
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: SimfTokens.space2),
        decoration: BoxDecoration(
          color: fill,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              _weekdayEn(date),
              style: TextStyle(
                color: weekdayColor,
                fontSize: SimfTokens.textXs,
                fontWeight: FontWeight.w600,
              ),
            ),
            Text(
              date.day.toString(),
              textAlign: TextAlign.center,
              style: TextStyle(
                color: numberColor,
                fontSize: SimfTokens.textMd,
                // Frame 883:2331 "Subheadline/semibold 14" — SemiBold, not bold.
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// One المواعيد timeline row (frame 1310:3213 collapsed / 1310:3232 featured):
/// a navy radius-8 card holding, inline-start→end, the content column (a 14px
/// gold calendar icon + the gold right-aligned title, the day banner on the
/// featured row, then the grey description) and a trailing vertical **time rail**
/// (start time over a hairline connector over the end time). No leading number,
/// no trailing chevron (the updated frame dropped both). The whole row taps
/// through to the session detail.
class _SessionRow extends StatelessWidget {
  const _SessionRow({
    required this.session,
    required this.isArabic,
    required this.onTap,
    this.featuredImageUrl,
  });

  final SessionListItem session;
  final bool isArabic;
  final VoidCallback onTap;
  final String? featuredImageUrl;

  @override
  Widget build(BuildContext context) {
    final description = session.localizedDescription(isArabic);
    final descriptionText = description == null
        ? null
        : Text(
            description,
            textAlign: TextAlign.start,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
              height: 1.4,
            ),
          );

    return SimfCard(
      onTap: onTap,
      // Frame 1310:3213 — radius 8, navy fill, no visible border.
      radius: SimfTokens.radius,
      borderWidth: 0,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space4,
          vertical: SimfTokens.space3,
        ),
        // IntrinsicHeight lets the leading time rail's connector stretch to the
        // full row height (title → description / banner).
        child: IntrinsicHeight(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              // Frame 883:2308 — the start/end time rail LEADS (inline-start =
              // physical right under RTL), with the content to its left.
              _TimeRail(start: session.startLocal, end: session.endLocal),
              const SizedBox(width: SimfTokens.space3),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    // Title line: 14px gold calendar glyph at the inline-start
                    // (right under RTL) + the gold right-aligned title.
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        const Padding(
                          padding: EdgeInsets.only(top: 1),
                          child: Icon(
                            Icons.calendar_today_outlined,
                            size: 14,
                            color: SimfTokens.accent,
                          ),
                        ),
                        const SizedBox(width: SimfTokens.space2),
                        Expanded(
                          child: Text(
                            session.localizedTitle(isArabic),
                            textAlign: TextAlign.start,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: SimfTokens.accent,
                              fontSize: SimfTokens.textMd,
                              fontWeight: FontWeight.w500,
                              height: 1.3,
                            ),
                          ),
                        ),
                      ],
                    ),
                    if (featuredImageUrl != null) ...<Widget>[
                      const SizedBox(height: SimfTokens.space3),
                      _DayBanner(imageUrl: featuredImageUrl),
                    ],
                    if (descriptionText != null) ...<Widget>[
                      const SizedBox(height: SimfTokens.space2),
                      descriptionText,
                    ],
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The trailing vertical time rail (frame 1310:3241): the start time at the top
/// (beige SemiBold), a thin beige connector down the middle, and the end time at
/// the bottom (white). Clock values stay LTR. 34px wide, full row height.
class _TimeRail extends StatelessWidget {
  const _TimeRail({required this.start, required this.end});

  final DateTime start;
  final DateTime end;

  static String _hhmm(DateTime t) =>
      '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}';

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 34,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: <Widget>[
          Text(
            _hhmm(start),
            textDirection: TextDirection.ltr,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w600,
            ),
          ),
          const Expanded(
            child: Padding(
              padding: EdgeInsets.symmetric(vertical: SimfTokens.space1),
              child: SizedBox(
                width: 1,
                child: ColoredBox(color: SimfTokens.beigeBorder40),
              ),
            ),
          ),
          Text(
            _hhmm(end),
            textDirection: TextDirection.ltr,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w400,
            ),
          ),
        ],
      ),
    );
  }
}

/// A short 3-letter English weekday for the day strip (LTR, as in the frame).
String _weekdayEn(DateTime day) {
  const names = <String>['MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT', 'SUN'];
  return names[day.weekday - 1];
}
