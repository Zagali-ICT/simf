import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/sessions/data/presentation_repository.dart';
import 'package:simf_app/features/sessions/data/sessions_repository.dart';
import 'package:simf_app/features/sessions/widgets/presentations_body.dart';

/// Session presentations — الجلسات ·
/// route: `RouteNames.sessionPresentations` · Figma 1388:7621
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

  Future<void> _refresh() => refreshAsync(ref, presentationsProvider.future);

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final presentations = ref.watch(presentationsProvider);
    // The programme drives the تحميل summary-ready gate; keyed by sessionId. It
    // is usually already cached (Home loaded it) — while it isn't, the map is
    // empty and [presentationSummaryReady] falls back to the row's own start.
    final sessionsById = ref.watch(programmeSessionsByIdProvider);

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
        data: (page) => PresentationsBody(
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
