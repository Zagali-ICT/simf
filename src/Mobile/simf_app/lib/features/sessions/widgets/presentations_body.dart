import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_refresh.dart';
import 'package:simf_app/app/widgets/simf_states.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/sessions/data/presentation_models.dart';
import 'package:simf_app/features/sessions/data/presentation_summary_gate.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/widgets/presentation_card.dart';
import 'package:simf_app/features/sessions/widgets/session_filter_tabs.dart';

class PresentationsBody extends StatelessWidget {
  const PresentationsBody({
    required this.items,
    required this.sessionsById,
    required this.dayTab,
    required this.onDayTab,
    required this.onRefresh,
    required this.l10n,
    super.key,
  });

  final List<PresentationItem> items;

  /// The cached programme keyed by `sessionId`, for the summary-ready gate
  /// ([presentationSummaryReady]). Empty while the programme is still loading.
  final Map<String, SessionListItem> sessionsById;
  final int dayTab;
  final ValueChanged<int> onDayTab;
  final Future<void> Function() onRefresh;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    if (items.isEmpty) {
      return SimfRefreshableMessage(
        onRefresh: onRefresh,
        child: SimfEmptyState(
          icon: Icons.description_outlined,
          message: l10n.presentationsEmpty,
        ),
      );
    }

    final isArabic = l10n.isArabic;
    final nowUtc = saudiNow();
    final days = distinctLocalDays(items, (p) => p.sessionStartLocal);
    final tabLabels = <String>[
      l10n.sessionsTabAll,
      for (var i = 0; i < days.length; i++) l10n.eventDayLabel(i + 1),
    ];
    // Guard against a stale selection when the day set shrinks on refresh.
    final activeTab = dayTab < tabLabels.length ? dayTab : 0;
    final visible = activeTab == 0
        ? items
        : items
            .where(
                (p) => sameLocalDay(p.sessionStartLocal, days[activeTab - 1]),)
            .toList(growable: false);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        const SizedBox(height: SimfTokens.space2),
        SessionFilterTabs(
          labels: tabLabels,
          selectedIndex: activeTab,
          onSelected: onDayTab,
        ),
        const SizedBox(height: SimfTokens.space3),
        Expanded(
          child: SimfPullToRefresh(
            onRefresh: onRefresh,
            child: ListView.separated(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(
                SimfTokens.space4,
                0,
                SimfTokens.space4,
                SimfTokens.space5,
              ),
              itemCount: visible.length,
              separatorBuilder: (_, __) =>
                  const SizedBox(height: SimfTokens.space3),
              itemBuilder: (context, index) {
                final item = visible[index];
                final dayIndex = days
                    .indexWhere((d) => sameLocalDay(item.sessionStartLocal, d));
                return PresentationCard(
                  item: item,
                  isArabic: isArabic,
                  dayLabel:
                      dayIndex >= 0 ? l10n.eventDayLabel(dayIndex + 1) : '',
                  summaryEnabled: presentationSummaryReady(
                    item,
                    sessionsById[item.sessionId],
                    nowUtc,
                  ),
                );
              },
            ),
          ),
        ),
      ],
    );
  }
}
