import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/sessions/data/sessions_repository.dart';
import 'package:simf_app/features/sessions/widgets/hub_list.dart';

/// Join a session hub — route: `RouteNames.joinSessionHub`
class JoinSessionHubScreen extends ConsumerWidget {
  const JoinSessionHubScreen({super.key});

  /// Pull-to-refresh — re-fetch the programme list (every data page supports
  /// the gesture, D-520/D-532; this screen was missing it — added D-601).
  Future<void> _refresh(WidgetRef ref) =>
      refreshAsync(ref, programmeSessionsProvider.future);

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final sessions = ref.watch(programmeSessionsProvider);
    return SimfPageShell(
      title: l10n.joinHubTitle,
      onBack: () => backOrHome(context),
      tab: SimfTab.sessions,
      body: sessions.when(
        loading: () => const SimfLoadingState(),
        // NO SimfPullableHost here — SimfRefreshableMessage already wraps its
        // child in one. Nesting two nests a SingleChildScrollView inside a
        // SingleChildScrollView, so the inner LayoutBuilder is handed
        // maxHeight: infinity and builds BoxConstraints(minHeight: infinity) —
        // "BoxConstraints forces an infinite height", which took the whole
        // screen down whenever the sessions provider errored. The empty branch
        // below was always correct; only this one double-wrapped.
        error: (_, __) => SimfRefreshableMessage(
          onRefresh: () => _refresh(ref),
          child: SimfErrorState(
            message: l10n.sessionsError,
            retryLabel: l10n.retryLabel,
            onRetry: () => ref.invalidate(programmeSessionsProvider),
          ),
        ),
        data: (items) => items.isEmpty
            ? SimfRefreshableMessage(
                onRefresh: () => _refresh(ref),
                child: SimfEmptyState(
                  icon: Icons.event_busy_outlined,
                  message: l10n.sessionsEmpty,
                ),
              )
            : SimfPullToRefresh(
                onRefresh: () => _refresh(ref),
                child: HubList(items: items, l10n: l10n),
              ),
      ),
    );
  }
}
