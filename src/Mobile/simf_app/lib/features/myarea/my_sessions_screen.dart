import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/myarea/data/my_sessions_models.dart';
import 'package:simf_app/features/myarea/data/my_sessions_repository.dart';
import 'package:simf_app/features/myarea/widgets/my_sessions_tabbed_list.dart';
import 'package:simf_app/features/sessions/widgets/session_filter_tabs.dart';

/// **My sessions** — App "تفاصيل الجلسات" (Figma 1388:9067, Approved account),
/// reached from the My-Area "my sessions" counter. The caller's booked / joined
/// sessions, partitioned into four tabs computed client-side from the device
/// clock: القادمة (still to come), حضرتها (attended), فاتتني (ended & not
/// attended), and الأرشيف (recorded / published). Each card carries the المفضلة
/// heart and taps through to the session detail. Reads `GET /app/account/sessions`.
///
/// Route: `RouteNames.myAreaSessions`.
/// Data: [mySessionsProvider].
/// Perf: ListView builds every child up front — correct for a short static
///       page, a defect on a data feed.
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
                child: MySessionsTabbedList(
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
