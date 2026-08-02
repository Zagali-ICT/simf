import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../core/utils/refresh.dart';
import 'data/presentation_models.dart';
import 'data/presentation_repository.dart';
import 'data/session_models.dart';
import 'data/sessions_repository.dart';
import 'widgets/session_filter_tabs.dart';
import 'package:simf_app/core/utils/saudi_time.dart';

/// **Sessions** — App "الجلسات" (Figma 1388:7621, Approved account), reached
/// from the Home "الجلسات" tile. Sessions grouped by event day, each card a file
/// icon + the session title + the presenting speaker + a gold تحميل button.
/// Owner 2026-07-03: tapping a card opens the **session detail** (17), and the
/// gold تحميل button opens that session's **summary** (ملخص الجلسة, 34) — this
/// screen no longer downloads the deck bytes. Reads `GET /app/presentations`.
///
/// Owner 2026-07-14: the تحميل button is **active only when a summary exists** —
/// a future/live session's محضر isn't published yet, so its button greys out
/// (inactive, not hidden). The presentations wire carries no summary flag, so the
/// gate joins each row to the cached programme ([programmeSessionsProvider]) by
/// `sessionId` and reads its `hasPublishedSummary` — matching the summaries-list
/// filter exactly ([presentationSummaryReady]).
class SessionPresentationsScreen extends ConsumerStatefulWidget {
  const SessionPresentationsScreen({super.key});

  @override
  ConsumerState<SessionPresentationsScreen> createState() =>
      _SessionPresentationsScreenState();
}

class _SessionPresentationsScreenState
    extends ConsumerState<SessionPresentationsScreen> {
  // 0 = الكل (all); 1..n = the nth distinct event day.
  int _dayTab = 0;

  /// Pull-to-refresh — re-fetch the presentations (invalidate + await next).
  Future<void> _refresh() => refreshAsync(ref, presentationsProvider.future);

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final presentations = ref.watch(presentationsProvider);
    // The programme drives the تحميل summary-ready gate; keyed by sessionId. It
    // is usually already cached (Home loaded it) — while it isn't, the map is
    // empty and [presentationSummaryReady] falls back to the row's own start.
    final sessionsById = <String, SessionListItem>{
      for (final s
          in ref.watch(programmeSessionsProvider).valueOrNull ??
              const <SessionListItem>[])
        s.id: s,
    };

    return SimfPageShell(
      title: l10n.sessionPresentationsTitle,
      onBack: () => backOrHome(context),
      body: presentations.when(
        loading: () => const SimfLoadingState(),
        error: (_, __) => SimfRefreshableMessage(
          onRefresh: _refresh,
          child: SimfErrorState(
            message: l10n.presentationsError,
            retryLabel: l10n.retryLabel,
            onRetry: () => ref.invalidate(presentationsProvider),
          ),
        ),
        data: (page) => _Body(
          items: page.items,
          sessionsById: sessionsById,
          dayTab: _dayTab,
          onDayTab: (i) => setState(() => _dayTab = i),
          onRefresh: _refresh,
          l10n: l10n,
        ),
      ),
    );
  }
}

/// Whether a presentation's تحميل (open-summary) button is active (owner
/// 2026-07-14). True only when the matched programme [session] has a published
/// summary — same signal the summaries list filters on. When the programme isn't
/// loaded yet ([session] null) it falls back to the presentation's own start: a
/// not-yet-started session can't have a summary, so its button stays inactive;
/// a started/past one keeps it (a real 404 shows the summary screen's empty note).
bool presentationSummaryReady(
  PresentationItem item,
  SessionListItem? session,
  DateTime nowUtc,
) =>
    session != null
        ? session.hasPublishedSummary
        : !nowUtc.isBefore(item.sessionStart);

class _Body extends StatelessWidget {
  const _Body({
    required this.items,
    required this.sessionsById,
    required this.dayTab,
    required this.onDayTab,
    required this.onRefresh,
    required this.l10n,
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
            .where((p) => sameLocalDay(p.sessionStartLocal, days[activeTab - 1]))
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
                final dayIndex =
                    days.indexWhere((d) => sameLocalDay(item.sessionStartLocal, d));
                return _PresentationCard(
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
class _PresentationCard extends StatelessWidget {
  const _PresentationCard({
    required this.item,
    required this.isArabic,
    required this.dayLabel,
    required this.summaryEnabled,
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
        queryParameters: <String, String>{RouteParams.sessionId: item.sessionId},
      );

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final speaker = item.localizedSpeaker(isArabic);

    return SimfCard(
      onTap: () => _openDetail(context),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2), // p-8 (Figma 1388:7640)
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
                        item.localizedSessionTitle(isArabic),
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
                const _FileIcon(),
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
                _SessionSummryButton(
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

/// The 32px navy file-icon box (Figma 1388:7643 — no border, a 20px beige glyph).
class _FileIcon extends StatelessWidget {
  const _FileIcon();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: SimfTokens.requestIconBox,
      height: SimfTokens.requestIconBox,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: const Icon(
        Icons.description_outlined,
        size: 20,
        color: SimfTokens.beigeBorder,
      ),
    );
  }
}

/// The gold ملخص الجلسة button (Figma 1388:7621) — opens the session summary
/// (34). When [enabled] is false (no published summary yet) it greys out with
/// the shell's disabled tokens and stops tapping — inactive, not hidden (owner
/// 2026-07-14, same treatment as the detail header's ملخص الجلسة button).
class _SessionSummryButton extends StatelessWidget {
  const _SessionSummryButton({
    required this.label,
    required this.enabled,
    required this.onTap,
  });

  final String label;
  final bool enabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final fg = enabled ? SimfTokens.surface : SimfTokens.navyDisabledText;
    final button = Material(
      color: enabled ? SimfTokens.accent : SimfTokens.navyDisabled,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: InkWell(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        onTap: enabled ? onTap : null,
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2), // p-8
          child: Text(
            label,
            style: TextStyle(
              color: fg,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
      ),
    );
    // Disabled: a deeper no-op tap recogniser wins the gesture arena over the
    // card's open-detail InkWell, so tapping an inactive button does nothing
    // (owner 2026-07-14) rather than falling through to the card.
    return enabled
        ? button
        : GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTap: () {},
            child: button,
          );
  }
}
