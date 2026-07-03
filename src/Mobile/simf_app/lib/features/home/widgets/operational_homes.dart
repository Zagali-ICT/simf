import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/route_names.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_bottom_nav.dart';
import '../../../app/widgets/simf_page_shell.dart';

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
              size: 32,
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
              size: 32,
              color: SimfTokens.accent,
            ),
            onTap: () => context.pushNamed(RouteNames.staffRegisterVisitor),
          ),
        ],
      ),
    );
  }
}

/// Moderator (محاور) home — a single entry into the sessions list, where the
/// moderator opens their session and runs its Q&A desk (reached from the session
/// detail; the server still enforces the per-session grant).
class ModeratorHome extends StatelessWidget {
  const ModeratorHome({required this.l10n, super.key});

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
            title: l10n.tileSessions,
            subtitle: l10n.moderatorManageQuestions,
            badgeOutlined: true,
            badge: const Icon(
              Icons.forum_outlined,
              size: 32,
              color: SimfTokens.accent,
            ),
            onTap: () => context.pushNamed(RouteNames.sessions),
          ),
        ],
      ),
    );
  }
}
