import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_cards.dart';
import 'package:simf_app/app/widgets/simf_refresh.dart';
import 'package:simf_app/app/widgets/simf_states.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/sessions/data/presentation_models.dart';
import 'package:simf_app/features/sessions/data/presentation_summary_gate.dart';
import 'package:simf_app/features/sessions/data/session_models.dart';
import 'package:simf_app/features/sessions/widgets/file_icon.dart';
import 'package:simf_app/features/sessions/widgets/session_filter_tabs.dart';
import 'package:simf_app/features/sessions/widgets/session_summry_button.dart';

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

/// One session card — tapping it opens the session detail (17); the gold تحميل
/// button opens that session's summary (34). Owner 2026-07-03.
class PresentationCard extends StatelessWidget {
  const PresentationCard({
    required this.item,
    required this.isArabic,
    required this.dayLabel,
    required this.summaryEnabled,
    super.key,
  });

  final PresentationItem item;
  final bool isArabic;
  final String dayLabel;

  /// Whether the تحميل button is active (a published summary exists) — see
  /// [presentationSummaryReady]. False greys it out and drops the tap.
  final bool summaryEnabled;

  /// Card tap → تفاصيل الجلسة (session detail, 17).
  void _openDetail(BuildContext context) => context.pushNamed(
        RouteNames.sessionDetail,
        pathParameters: <String, String>{RouteParams.sessionId: item.sessionId},
      );

  /// تحميل → ملخص الجلسة (session summary, 34). 404s gracefully until the
  /// Committee publishes the summary.
  void _openSummary(BuildContext context) => context.pushNamed(
        RouteNames.aiSummary,
        queryParameters: <String, String>{
          RouteParams.sessionId: item.sessionId,
        },
      );

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final speaker = item.localizedSpeaker(isArabic: isArabic);

    return SimfCard(
      onTap: () => _openDetail(context),
      child: Padding(
        padding:
            const EdgeInsets.all(SimfTokens.space2), // p-8 (Figma 1388:7640)
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            // Title + speaker on the right, the file icon on the left.
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: <Widget>[
                      Text(
                        item.localizedSessionTitle(isArabic: isArabic),
                        textAlign: TextAlign.start,
                        style: SimfTokens.labelWhiteMedium,
                      ),
                      if (speaker != null) ...<Widget>[
                        const SizedBox(height: SimfTokens.space2),
                        Text(
                          speaker,
                          textAlign: TextAlign.start,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: SimfTokens.labelBeigeSm,
                        ),
                      ],
                    ],
                  ),
                ),
                const SizedBox(width: SimfTokens.space3),
                const FileIcon(),
              ],
            ),
            const SizedBox(height: SimfTokens.space6), // gap-24
            // Summary button on the left, the event-day label on the right.
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: <Widget>[
                if (dayLabel.isNotEmpty)
                  Flexible(
                    child: Text(
                      dayLabel,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: SimfTokens.labelBeigeSm,
                    ),
                  )
                else
                  const SizedBox.shrink(),
                SessionSummryButton(
                  label: l10n.sessionSummary,
                  enabled: summaryEnabled,
                  onTap: () => _openSummary(context),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
