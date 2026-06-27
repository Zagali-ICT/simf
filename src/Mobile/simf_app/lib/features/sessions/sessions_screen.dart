import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart' show DateFormat;
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../app/widgets/simf_svg_icon.dart';
import 'data/session_models.dart';
import 'data/sessions_repository.dart';

// The time-chip's two lines, formatted once (hoisted off the build path).
// Pinned to 'en' so the chip shows Western digits + Latin AM/PM in both locales
// (frame 1064:13230 — "08:00 / PM", not Arabic-Indic digits or ص/م).
final DateFormat _chipHourFormat = DateFormat('hh:mm', 'en');
final DateFormat _chipPeriodFormat = DateFormat('a', 'en');

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
    return KsaPage(
      // Frame 883:2314 — the screen header is "برنامج الملتقى" (the bottom-nav
      // tab keeps its own "الأجندة" label).
      title: l10n.sessionsProgrammeTitle,
      onBack: () => ksaBackOrHome(context),
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
      return KsaRefresh(
        onRefresh: _load,
        child: LayoutBuilder(
          builder: (context, constraints) => SingleChildScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            child: ConstrainedBox(
              constraints: BoxConstraints(minHeight: constraints.maxHeight),
              child: KsaErrorState(
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
      return KsaRefresh(
        onRefresh: _load,
        child: LayoutBuilder(
          builder: (context, constraints) => SingleChildScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            child: ConstrainedBox(
              constraints: BoxConstraints(minHeight: constraints.maxHeight),
              child: KsaEmptyState(
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

    return KsaRefresh(
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
          KsaSectionHeader(title: selected.localizedTitle(isArabic)),
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
          KsaSectionHeader(title: l10n.sessionsScheduleSection),
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
                index: index + 1,
                isArabic: isArabic,
                // The first session of the day is featured — expanded with the
                // day banner image (frame 1064:13222 / 1064:13233).
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
        suffixIcon: const SimfSvgIcon(
          'assets/icons/ic_search.svg',
          size: 18,
          color: Colors.white,
        ),
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
    // Frame order (RTL, right→left): ورش العمل · جلسات · احداث · الكل. A Row lays
    // children start→end, so the order here is workshops → sessions → events → all.
    final tabs = <(String, SessionType?)>[
      (l10n.sessionTypeWorkshop, SessionType.workshop),
      (l10n.sessionTypeSession, SessionType.session),
      (l10n.sessionTypeEvent, SessionType.event),
      (l10n.sessionTypeAll, null),
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
    return KsaCard(
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
                  colors: <Color>[Colors.transparent, Color(0xCC001030)],
                ),
              ),
            ),
            // The gold anchor badge (frame node 1064:13249).
            PositionedDirectional(
              top: SimfTokens.space2,
              end: SimfTokens.space2,
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
        color: SimfTokens.calendarBand,
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
    // #4 — selected = black; a day with sessions ("active") = white; an empty
    // in-between day = muted grey (transparent, so the band shows through).
    final Color fill;
    final Color textColor;
    if (selected) {
      fill = SimfTokens.navy; // near-black
      textColor = Colors.white;
    } else if (hasSessions) {
      fill = SimfTokens.surface; // white
      textColor = SimfTokens.navy;
    } else {
      fill = Colors.transparent;
      textColor = SimfTokens.greyText;
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
                color: textColor,
                fontSize: SimfTokens.textXs,
                fontWeight: FontWeight.w600,
              ),
            ),
            Text(
              date.day.toString(),
              textAlign: TextAlign.center,
              style: TextStyle(
                color: textColor,
                fontSize: SimfTokens.textMd,
                fontWeight: FontWeight.w700,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// One agenda row (frame nodes 883:2364 collapsed / 1064:13222 featured): the
/// numbered gold title + grey description with a two-line time chip and a gold
/// chevron. The **featured** first row (when [featuredImageUrl] is set) also
/// shows the day banner image between the title and the description.
class _SessionRow extends StatelessWidget {
  const _SessionRow({
    required this.session,
    required this.index,
    required this.isArabic,
    required this.onTap,
    this.featuredImageUrl,
  });

  final SessionListItem session;
  final int index;
  final bool isArabic;
  final VoidCallback onTap;
  final String? featuredImageUrl;

  @override
  Widget build(BuildContext context) {
    final description = session.localizedDescription(isArabic);
    final titleRow = Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: <Widget>[
        _TimeChip(local: session.startLocal),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: Text.rich(
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
        ),
        const SizedBox(width: SimfTokens.space2),
        // Frame 883:2308 — gold iconamoon chevron, not a white Material one.
        const SimfSvgIcon(
          'assets/icons/ic_back.svg',
          size: 20,
          color: SimfTokens.accent,
        ),
      ],
    );

    final descriptionText = description == null
        ? null
        : Text(
            description,
            maxLines: featuredImageUrl != null ? 3 : 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
              height: 1.5,
            ),
          );

    return KsaCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space2,
          vertical: SimfTokens.space4,
        ),
        child: featuredImageUrl != null
            // Featured: title row + the day banner image + description below.
            ? Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  titleRow,
                  const SizedBox(height: SimfTokens.space3),
                  _DayBanner(imageUrl: featuredImageUrl),
                  if (descriptionText != null) ...<Widget>[
                    const SizedBox(height: SimfTokens.space3),
                    descriptionText,
                  ],
                ],
              )
            // Collapsed: time chip + numbered title + description, chevron at end.
            : Row(
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
                        if (descriptionText != null) ...<Widget>[
                          const SizedBox(height: SimfTokens.space2),
                          descriptionText,
                        ],
                      ],
                    ),
                  ),
                  const SizedBox(width: SimfTokens.space2),
                  const SimfSvgIcon(
                    'assets/icons/ic_back.svg',
                    size: 20,
                    color: SimfTokens.accent,
                  ),
                ],
              ),
      ),
    );
  }
}

/// The two-line bordered time chip (frame node 1064:13230): `08:00` over `PM`.
class _TimeChip extends StatelessWidget {
  const _TimeChip({required this.local});

  final DateTime local;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      child: SizedBox(
        width: 52,
        height: 48,
        child: Center(
          child: Text(
            '${_chipHourFormat.format(local)}\n${_chipPeriodFormat.format(local)}',
            textAlign: TextAlign.center,
            textDirection: TextDirection.ltr,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w700,
              height: 1.4,
            ),
          ),
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
