import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/route_names.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_bottom_nav.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../../moderation/data/moderation_models.dart';
import '../../moderation/data/moderation_repository.dart';
import '../../moderation/widgets/moderated_session_tile.dart';

/// Staff (gate) home — the two gate operations: scan a badge + register a
/// walk-in visitor. The attendee experience is intentionally absent (D-519).
class StaffHome extends StatelessWidget {
  const StaffHome({required this.l10n, super.key});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return SimfPageShell(
      tab: SimfTab.home,
      title: l10n.homeTitle,
      body: ListView(
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          SimfListRow(
            title: l10n.gateScannerEntry,
            badgeOutlined: true,
            badge: const Icon(
              Icons.qr_code_scanner,
              size: SimfTokens.operationalHomesSize,
              color: SimfTokens.accent,
            ),
            onTap: () => context.pushNamed(RouteNames.gateScanner),
          ),
          const SizedBox(height: SimfTokens.space4),
          SimfListRow(
            title: l10n.staffRegisterVisitorEntry,
            badgeOutlined: true,
            badge: const Icon(
              Icons.person_add_alt_1_outlined,
              size: SimfTokens.operationalHomesSize,
              color: SimfTokens.accent,
            ),
            onTap: () => context.pushNamed(RouteNames.staffRegisterVisitor),
          ),
        ],
      ),
    );
  }
}

/// Moderator (محاور) home — the whole programme (to browse), plus **جلساتي**:
/// the sessions the moderator actually holds a per-session `SessionModerator`
/// grant on, each tapping straight through to its Q&A desk.
///
/// FR-MOD-001 — before this list the moderator had no way to DISCOVER their
/// grants: home offered only "all sessions", session detail showed the desk
/// action on every one of them, and a missing grant surfaced as a 403 after the
/// tap. `GET /app/sessions/moderated` (`myModeratedSessionsProvider`) is that
/// discovery list; the same provider gates the session-detail affordance.
class ModeratorHome extends ConsumerWidget {
  const ModeratorHome({required this.l10n, super.key});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return SimfPageShell(
      tab: SimfTab.home,
      title: l10n.homeTitle,
      body: SimfPullToRefresh(
        onRefresh: () async {
          ref.invalidate(myModeratedSessionsProvider);
          await ref.read(myModeratedSessionsProvider.future);
        },
        child: ref.watch(myModeratedSessionsProvider).when(
              loading: () => const Center(
                child: CircularProgressIndicator(color: SimfTokens.accent),
              ),
              error: (_, __) => SimfPullableHost(
                child: SimfErrorState(
                  message: l10n.moderatorMySessionsError,
                  retryLabel: l10n.retryLabel,
                  onRetry: () => ref.invalidate(myModeratedSessionsProvider),
                ),
              ),
              data: (sessions) => _body(context, sessions),
            ),
      ),
    );
  }

  Widget _body(BuildContext context, List<ModeratedSession> sessions) {
    // One always-scrollable list: the programme entry, the جلساتي heading, then
    // a row per granted session (or the empty note in their place).
    return ListView.separated(
      padding: const EdgeInsets.all(SimfTokens.space4),
      physics: const AlwaysScrollableScrollPhysics(),
      itemCount: sessions.isEmpty ? 3 : sessions.length + 2,
      separatorBuilder: (_, __) => const SizedBox(height: SimfTokens.space4),
      itemBuilder: (context, index) {
        if (index == 0) {
          return SimfListRow(
            title: l10n.tileSessions,
            subtitle: l10n.moderatorManageQuestions,
            badgeOutlined: true,
            badge: const Icon(
              Icons.forum_outlined,
              size: SimfTokens.operationalHomesSize,
              color: SimfTokens.accent,
            ),
            onTap: () => context.pushNamed(RouteNames.sessions),
          );
        }
        if (index == 1) {
          return SimfSectionHeader(title: l10n.moderatorMySessions);
        }
        if (sessions.isEmpty) {
          return SimfPageNote(text: l10n.moderatorMySessionsEmpty);
        }
        final session = sessions[index - 2];
        return ModeratedSessionTile(
          l10n: l10n,
          session: session,
          onTap: () => context.pushNamed(
            RouteNames.sessionModerate,
            pathParameters: <String, String>{
              RouteParams.sessionId: session.sessionId,
            },
          ),
        );
      },
    );
  }
}
