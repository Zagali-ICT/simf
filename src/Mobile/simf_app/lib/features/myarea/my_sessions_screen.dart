import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../core/utils/refresh.dart';
import '../sessions/data/session_lifecycle.dart';
import '../sessions/widgets/favourite_heart_button.dart';
import '../sessions/widgets/session_card_meta.dart';
import '../sessions/widgets/session_filter_tabs.dart';
import '../sessions/widgets/session_state_chip.dart';
import 'data/my_sessions_models.dart';
import 'data/my_sessions_repository.dart';
import 'package:simf_app/core/utils/saudi_time.dart';

/// **My sessions** — App "تفاصيل الجلسات" (Figma 1388:9067, Approved account),
/// reached from the My-Area "my sessions" counter. The caller's booked / joined
/// sessions, partitioned into four tabs computed client-side from the device
/// clock: القادمة (still to come), حضرتها (attended), فاتتني (ended & not
/// attended), and الأرشيف (recorded / published). Each card carries the المفضلة
/// heart and taps through to the session detail. Reads `GET /app/account/sessions`.
class MySessionsScreen extends ConsumerStatefulWidget {
  const MySessionsScreen({super.key});

  @override
  ConsumerState<MySessionsScreen> createState() => _MySessionsScreenState();
}

enum _MySessionsTab { upcoming, attended, missed, archive }

class _MySessionsScreenState extends ConsumerState<MySessionsScreen> {
  _MySessionsTab _tab = _MySessionsTab.upcoming;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final sessions = ref.watch(mySessionsProvider);

    final tabLabels = <String>[
      l10n.mySessionsTabUpcoming,
      l10n.mySessionsTabAttended,
      l10n.mySessionsTabMissed,
      l10n.mySessionsTabArchive,
    ];

    Future<void> onRefresh() => refreshAsync(ref, mySessionsProvider.future);

    return SimfPageShell(
      title: l10n.mySessionsTitle,
      onBack: () => backOrHome(context),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const SizedBox(height: SimfTokens.space2),
          SessionFilterTabs(
            labels: tabLabels,
            selectedIndex: _MySessionsTab.values.indexOf(_tab),
            onSelected: (i) =>
                setState(() => _tab = _MySessionsTab.values[i]),
            // The frame (1388:9077) has 4 equal-width tabs (gap-8) each with a
            // leading glyph.
            equalWidth: true,
            gap: SimfTokens.space2,
            icons: const <IconData>[
              Icons.upcoming_outlined, // القادمة
              Icons.event_available_outlined, // حضرتها
              Icons.event_busy_outlined, // فاتتني
              Icons.archive_outlined, // الأرشيف
            ],
          ),
          const SizedBox(height: SimfTokens.space3),
          Expanded(
            child: sessions.when(
              loading: () => const SimfLoadingState(),
              error: (_, __) => SimfPullToRefresh(
                onRefresh: onRefresh,
                child: ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  children: <Widget>[
                    SimfErrorState(
                      message: l10n.mySessionsError,
                      retryLabel: l10n.retryLabel,
                      onRetry: () => ref.invalidate(mySessionsProvider),
                    ),
                  ],
                ),
              ),
              data: (page) => SimfPullToRefresh(
                onRefresh: onRefresh,
                child: _TabbedList(
                  items: _filter(page.items),
                  tabLabel: tabLabels[_MySessionsTab.values.indexOf(_tab)],
                  l10n: l10n,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  List<MyAreaSessionItem> _filter(List<MyAreaSessionItem> items) {
    final nowUtc = saudiNow();
    return items.where((item) {
      switch (_tab) {
        case _MySessionsTab.upcoming:
          return item.isUpcoming(nowUtc);
        case _MySessionsTab.attended:
          return item.attended;
        case _MySessionsTab.missed:
          return item.hasEnded(nowUtc) && !item.attended;
        case _MySessionsTab.archive:
          return item.isArchived;
      }
    }).toList(growable: false);
  }
}

class _TabbedList extends StatelessWidget {
  const _TabbedList({
    required this.items,
    required this.tabLabel,
    required this.l10n,
  });

  final List<MyAreaSessionItem> items;
  final String tabLabel;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    if (items.isEmpty) {
      return ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        children: <Widget>[
          SimfEmptyState(
            icon: Icons.event_note_outlined,
            message: l10n.mySessionsEmpty,
          ),
        ],
      );
    }
    final isArabic = l10n.isArabic;
    return ListView.separated(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        0,
        SimfTokens.space4,
        SimfTokens.space5,
      ),
      itemCount: items.length + 1,
      separatorBuilder: (_, __) => const SizedBox(height: SimfTokens.space3),
      itemBuilder: (context, index) {
        if (index == 0) {
          return Padding(
            padding: const EdgeInsets.only(bottom: SimfTokens.space2),
            child: Text(
              l10n.mySessionsCount(items.length, tabLabel),
              style: const TextStyle(
                color: SimfTokens.surface,
                fontSize: SimfTokens.textLg, // 16
                fontWeight: FontWeight.w500,
              ),
            ),
          );
        }
        return _MySessionCard(item: items[index - 1], isArabic: isArabic);
      },
    );
  }
}

/// One my-session card: the heart on the trailing edge, the title over a
/// clock·time line with the category chip, and the primary speaker + hall.
class _MySessionCard extends StatelessWidget {
  const _MySessionCard({required this.item, required this.isArabic});

  final MyAreaSessionItem item;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final time = TimeOfDay.fromDateTime(item.startLocal).format(context);
    final category = item.localizedCategory(isArabic);
    final speaker = item.localizedSpeaker(isArabic);
    final hall = item.localizedHall(isArabic);
    final timeText = (category != null && category.isNotEmpty)
        ? '$time · $category'
        : time;
    final hasMeta = speaker != null || (hall != null && hall.isNotEmpty);
    // Owner 2026-07-14 — the same state chips as the agenda (my-sessions carries
    // no summary flag, so only live-now / recorded show here).
    final phase = sessionPhase(item.start, item.end, saudiNow());
    final stateChips = sessionStateChips(
      phase: phase,
      hasPublishedSummary: false,
      status: item.status,
    );

    return SimfCard(
      onTap: () => context.pushNamed(
        RouteNames.sessionDetail,
        pathParameters: <String, String>{RouteParams.sessionId: item.id},
      ),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2), // p-8 (Figma 1388:9115)
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            // Title + time·category on the right, the favourite heart on the left.
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: <Widget>[
                      Text(
                        item.localizedTitle(isArabic),
                        textAlign: TextAlign.start,
                        style: const TextStyle(
                          color: SimfTokens.surface,
                          fontWeight: FontWeight.w500,
                          fontSize: SimfTokens.textMd, // 14
                        ),
                      ),
                      const SizedBox(height: SimfTokens.space2),
                      SessionIconLine(icon: Icons.access_time, text: timeText),
                    ],
                  ),
                ),
                const SizedBox(width: SimfTokens.space2),
                FavouriteHeartButton(sessionId: item.id),
              ],
            ),
            // Speaker (right) + hall (left), each in a beige icon-box group.
            if (hasMeta) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: <Widget>[
                  if (speaker != null)
                    Flexible(
                      child: SessionMetaGroup(
                        icon: Icons.person_outline,
                        text: _speakerText(speaker),
                      ),
                    ),
                  if (hall != null && hall.isNotEmpty)
                    Flexible(
                      child: SessionMetaGroup(
                        icon: Icons.location_on_outlined,
                        text: hall,
                      ),
                    ),
                ],
              ),
            ],
            if (stateChips.isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space3),
              SessionStateChipRow(kinds: stateChips, l10n: AppL10n.of(context)),
            ],
          ],
        ),
      ),
    );
  }

  String _speakerText(String speaker) {
    final title = item.speakerTitle?.trim();
    return title == null || title.isEmpty ? speaker : '$speaker · $title';
  }
}
