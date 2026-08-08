import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../core/utils/refresh.dart';
import 'data/presentation_repository.dart';
import 'data/presentation_summary_gate.dart';
import 'data/session_models.dart';
import 'data/sessions_repository.dart';
import 'widgets/presentations_body.dart';

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

