import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/net/asset_urls.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/widgets/programme_day_banner.dart';
import 'package:simf_app/features/sessions/widgets/programme_day_strip.dart';
import 'package:simf_app/features/sessions/widgets/session_timeline_row.dart';
import 'package:simf_app/features/sessions/widgets/session_type_tabs.dart';
import 'package:simf_app/features/sessions/widgets/sessions_search_field.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The day to open on: the first that actually HAS sessions, not blindly the
/// first — otherwise a programme whose sessions sit on a later day renders an
/// empty schedule until the user taps that day by hand.
ProgrammeDay _defaultDay(List<ProgrammeDay> days) => days.firstWhere(
      (day) => day.sessions.isNotEmpty,
      orElse: () => days.first,
    );

/// The loaded Sessions screen (Figma 883:2308): the search field, the day
/// strip, the selected day's title + banner, the type tabs, and the المواعيد
/// schedule.
///
/// The screen owns the day / type / query selection and the navigation; this
/// owns the layout that reads them.
class ProgrammeBody extends ConsumerWidget {
  const ProgrammeBody({
    required this.days,
    required this.l10n,
    required this.selectedDayId,
    required this.typeFilter,
    required this.query,
    required this.emptyMessage,
    required this.onQueryChanged,
    required this.onDayChanged,
    required this.onTypeChanged,
    required this.onRefresh,
    required this.onOpenSession,
    super.key,
  });

  final List<ProgrammeDay> days;
  final AppL10n l10n;

  /// The day the user picked, or null to fall back to [_defaultDay].
  final String? selectedDayId;

  /// The active type tab; null = الكل / All.
  final SessionType? typeFilter;
  final String query;

  /// The "nothing scheduled" line for the active type tab, phrased by the
  /// screen.
  final String emptyMessage;

  final ValueChanged<String> onQueryChanged;
  final ValueChanged<String> onDayChanged;
  final ValueChanged<SessionType?> onTypeChanged;
  final Future<void> Function() onRefresh;
  final ValueChanged<SessionListItem> onOpenSession;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isArabic = l10n.isArabic;
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    // The default is resolved HERE rather than written into state when the
    // load returns. Same first render, and a pull-to-refresh no longer throws
    // away the day the user picked — `_load` used to overwrite the selection
    // on every reload.
    final selected = days.firstWhere(
      (d) => d.id == selectedDayId,
      orElse: () => _defaultDay(days),
    );
    final dayImageUrl = selected.hasImage
        ? AssetUrls.image(baseUrl, AssetKind.programmeDayImage, selected.id)
        : null;

    // The selected day's sessions, filtered by the active type tab + the
    // search.
    final sessions = sessionsForDay(
      selected,
      type: typeFilter,
      query: query,
    );

    // The chrome is a fixed handful of widgets; the schedule is one row per
    // session for a server-driven day, so it builds lazily in its own sliver
    // rather than being spread eagerly into the same list (section 4).
    return SimfPullToRefresh(
      onRefresh: onRefresh,
      child: CustomScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        slivers: <Widget>[
          SliverPadding(
            // Top gap below the header matches Figma 883:2308 (title row →
            // search ≈ 32px; the shell header adds its own 8px, so the body
            // adds ~24px) — owner 2026-06-30: the header's bottom space was
            // too small.
            padding: const EdgeInsets.fromLTRB(
              SimfTokens.space4,
              SimfTokens.space6,
              SimfTokens.space4,
              0,
            ),
            sliver: SliverList(
              delegate: SliverChildListDelegate(<Widget>[
                SessionsSearchField(
                  l10n: l10n,
                  onChanged: onQueryChanged,
                ),
                const SizedBox(height: SimfTokens.space4),
                ProgrammeDayStrip(
                  days: days,
                  selectedId: selected.id,
                  onChanged: onDayChanged,
                ),
                const SizedBox(height: SimfTokens.space5),
                // تفاصيل اليوم (883:2327 area) — the selected day's OWN title +
                // its logo banner. The "تفاصيل اليوم" label carries the day
                // title (owner: not a static label — it is the day's title).
                SimfSectionHeader(
                  title: selected.localizedTitle(isArabic: isArabic),
                ),
                const SizedBox(height: SimfTokens.space3),
                ProgrammeDayBanner(imageUrl: dayImageUrl),
                const SizedBox(height: SimfTokens.space5),
                // Type tabs (883:2320): الكل / جلسات / ورش العمل.
                SessionTypeTabs(
                  l10n: l10n,
                  active: typeFilter,
                  onChanged: onTypeChanged,
                ),
                const SizedBox(height: SimfTokens.space5),
                SimfSectionHeader(title: l10n.sessionsScheduleSection),
                const SizedBox(height: SimfTokens.space3),
                if (sessions.isEmpty)
                  Padding(
                    padding: const EdgeInsets.symmetric(
                      vertical: SimfTokens.space4,
                    ),
                    child: Text(
                      emptyMessage,
                      style: SimfTokens.hintBeige,
                    ),
                  ),
              ]),
            ),
          ),
          SliverPadding(
            padding: const EdgeInsets.fromLTRB(
              SimfTokens.space4,
              0,
              SimfTokens.space4,
              SimfTokens.space6,
            ),
            sliver: SliverList.builder(
              itemCount: sessions.length,
              itemBuilder: (context, index) {
                final session = sessions[index];
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    if (index > 0) const SizedBox(height: SimfTokens.space4),
                    SessionTimelineRow(
                      session: session,
                      isArabic: isArabic,
                      // The first session of the day is featured — expanded
                      // with the day banner image (frame 1310:3232).
                      featuredImageUrl: index == 0 ? dayImageUrl : null,
                      onTap: () => onOpenSession(session),
                    ),
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
