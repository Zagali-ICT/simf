import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_filter_search_field.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../myarea/data/my_sessions_repository.dart';
import '../sessions/data/session_favourites.dart';
import '../sessions/data/session_models.dart';
import '../sessions/data/sessions_repository.dart' show programmeSessionsProvider;
import '../sessions/widgets/session_filter_tabs.dart';
import 'widgets/session_summary_list_card.dart';

/// **Session summaries** — App "ملخص الجلسات" (Figma 1388:8392, Guest+). Every
/// programme session in a searchable, day-grouped list with three tabs —
/// الجميع (all), جلساتي (the caller's booked sessions), المفضلة (favourited) —
/// and the المفضلة heart on each card. Tapping a card opens that session's
/// AI-summary details (#34). Reuses the cached programme (`programmeSessionsProvider`);
/// the booked set + favourites come from the approved-account reads (empty for a
/// guest).
class SessionSummaryListScreen extends ConsumerStatefulWidget {
  const SessionSummaryListScreen({super.key});

  @override
  ConsumerState<SessionSummaryListScreen> createState() =>
      _SessionSummaryListScreenState();
}

enum _SummaryTab { all, mine, favourites }

class _SessionSummaryListScreenState
    extends ConsumerState<SessionSummaryListScreen> {
  _SummaryTab _tab = _SummaryTab.all;
  String _query = '';

  /// Pull-to-refresh — re-fetch the programme (invalidate + await next).
  Future<void> _refresh() async {
    ref.invalidate(programmeSessionsProvider);
    await ref.read(programmeSessionsProvider.future);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final sessions = ref.watch(programmeSessionsProvider);

    final tabLabels = <String>[
      l10n.sessionsTabAll,
      l10n.sessionsTabMine,
      l10n.sessionsTabFavourites,
    ];

    return SimfPageShell(
      title: l10n.sessionSummariesTitle,
      onBack: () => backOrHome(context),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const SizedBox(height: SimfTokens.space3),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
            child: SimfFilterSearchField(
              hint: l10n.sessionSummarySearchHint,
              onChanged: (value) => setState(() => _query = value),
            ),
          ),
          const SizedBox(height: SimfTokens.space3),
          SessionFilterTabs(
            labels: tabLabels,
            selectedIndex: _SummaryTab.values.indexOf(_tab),
            onSelected: (i) => setState(() => _tab = _SummaryTab.values[i]),
            // The summaries frame (1388:8392) has exactly 3 equal-width tabs.
            equalWidth: true,
          ),
          const SizedBox(height: SimfTokens.space3),
          Expanded(
            child: sessions.when(
              loading: () => const Center(
                child: CircularProgressIndicator(color: SimfTokens.accent),
              ),
              error: (_, __) => SimfPullToRefresh(
                onRefresh: _refresh,
                child: SimfPullableHost(
                  child: SimfErrorState(
                    message: l10n.aiSummaryError,
                    retryLabel: l10n.retryLabel,
                    onRetry: () => ref.invalidate(programmeSessionsProvider),
                  ),
                ),
              ),
              data: (items) => _buildList(context, l10n, items),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildList(
    BuildContext context,
    AppL10n l10n,
    List<SessionListItem> items,
  ) {
    // Re-run the build (and so the filter + hearts) when the per-user sets
    // resolve or change, keeping the جلساتي / المفضلة tabs live.
    ref.watch(sessionFavouritesProvider);
    ref.watch(mySessionsProvider);

    final isArabic = l10n.isArabic;
    final filtered = _filter(items);
    if (filtered.isEmpty) {
      return SimfPullToRefresh(
        onRefresh: _refresh,
        child: SimfPullableHost(
          child: SimfEmptyState(
            icon: Icons.summarize_outlined,
            message: _emptyMessage(l10n, items.isEmpty),
          ),
        ),
      );
    }

    final days = _distinctDays(filtered);

    return SimfPullToRefresh(
      onRefresh: _refresh,
      child: ListView.builder(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          0,
          SimfTokens.space4,
          SimfTokens.space5,
        ),
        itemCount: days.length,
        itemBuilder: (context, dayIndex) {
          final day = days[dayIndex];
          final dayItems = filtered
              .where((s) => _sameDay(s.startLocal, day))
              .toList(growable: false);
          return Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Padding(
                padding: const EdgeInsets.only(
                  top: SimfTokens.space2,
                  bottom: SimfTokens.space2,
                ),
                child: Text(
                  l10n.eventDayLabel(dayIndex + 1),
                  // Frame 1388:8428 — day header is Inter Medium (w500), not w600.
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: SimfTokens.textLg,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ),
              for (final item in dayItems)
                Padding(
                  padding: const EdgeInsets.only(bottom: SimfTokens.space3),
                  child: SessionSummaryCard(
                    item: item,
                    isArabic: isArabic,
                    l10n: l10n,
                    durationLabel: l10n.sessionDurationMinutes(
                      _durationMinutes(item),
                    ),
                  ),
                ),
            ],
          );
        },
      ),
    );
  }

  List<SessionListItem> _filter(List<SessionListItem> items) {
    final favouriteIds =
        ref.read(sessionFavouritesProvider).valueOrNull ?? const <String>{};
    final mineIds = ref.read(mySessionsProvider).valueOrNull?.items
            .map((s) => s.id)
            .toSet() ??
        const <String>{};
    final needle = _query.trim().toLowerCase();

    return items.where((session) {
      // Owner 2026-07-14 — the summaries list is ONLY sessions with a published
      // محضر; a future / not-yet-summarised session must not appear here.
      if (!session.hasPublishedSummary) {
        return false;
      }
      switch (_tab) {
        case _SummaryTab.mine:
          if (!mineIds.contains(session.id)) {
            return false;
          }
        case _SummaryTab.favourites:
          if (!favouriteIds.contains(session.id)) {
            return false;
          }
        case _SummaryTab.all:
          break;
      }
      if (needle.isEmpty) {
        return true;
      }
      final haystack = <String?>[
        session.title,
        session.titleArabic,
        for (final speaker in session.speakers) speaker.name,
        for (final speaker in session.speakers) speaker.nameArabic,
      ].whereType<String>().join(' ').toLowerCase();
      return haystack.contains(needle);
    }).toList(growable: false);
  }

  String _emptyMessage(AppL10n l10n, bool noSessionsAtAll) {
    if (noSessionsAtAll) {
      return l10n.aiSummaryNoSessions;
    }
    if (_query.trim().isNotEmpty) {
      return l10n.sessionsNoMatch;
    }
    switch (_tab) {
      case _SummaryTab.mine:
        return l10n.sessionsNoMine;
      case _SummaryTab.favourites:
        return l10n.sessionsNoFavourites;
      case _SummaryTab.all:
        // The programme has sessions but none are summarised yet.
        return l10n.sessionSummariesEmpty;
    }
  }

  List<DateTime> _distinctDays(List<SessionListItem> items) {
    final byKey = <String, DateTime>{};
    for (final s in items) {
      final local = s.startLocal;
      final key = '${local.year}-${local.month}-${local.day}';
      byKey.putIfAbsent(key, () => DateTime(local.year, local.month, local.day));
    }
    final days = byKey.values.toList()..sort();
    return days;
  }

  bool _sameDay(DateTime a, DateTime b) =>
      a.year == b.year && a.month == b.month && a.day == b.day;

  int _durationMinutes(SessionListItem item) {
    final minutes = item.endUtc.difference(item.startUtc).inMinutes;
    return minutes < 0 ? 0 : minutes;
  }
}
