import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../core/utils/refresh.dart';
import 'data/session_models.dart';
import 'data/sessions_repository.dart';
import 'widgets/hub_row.dart';

/// D-485 — **Join a session** hub (`/sessions/join`, approved Visitor). The
/// standalone entry into the join flow (the other entry is the Join CTA on the
/// session page, per the owner's "both" choice): it lists the programme
/// sessions; tapping one opens its detail page, where the **Select my seat /
/// Join** CTA lives. Reuses `GET /app/programme/sessions` — no new API.
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
                child: _HubList(items: items, l10n: l10n),
              ),
      ),
    );
  }
}

class _HubList extends StatelessWidget {
  const _HubList({required this.items, required this.l10n});

  final List<SessionListItem> items;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    return ListView.separated(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space3,
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
              l10n.joinHubHint,
              textAlign: TextAlign.center,
              style: SimfTokens.labelBeigeSm,
            ),
          );
        }
        final item = items[index - 1];
        return HubRow(
          title: item.localizedTitle(isArabic),
          subtitle: _subtitle(context, item, isArabic),
          onTap: () => context.pushNamed(
            RouteNames.sessionDetail,
            pathParameters: <String, String>{RouteParams.sessionId: item.id},
          ),
        );
      },
    );
  }

  String _subtitle(BuildContext context, SessionListItem item, bool isArabic) {
    final time = TimeOfDay.fromDateTime(item.startLocal).format(context);
    final hall = item.localizedHall(isArabic);
    return hall == null || hall.isEmpty ? time : '$time · $hall';
  }
}

