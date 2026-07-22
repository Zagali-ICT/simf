import 'package:flutter/material.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_app_shell.dart' show SimfShellScope, tabIndex;
import '../../../app/widgets/simf_bottom_nav.dart' show SimfTab;
import '../../../app/widgets/simf_page_shell.dart';

/// The greeting header (frame node 203:1238): avatar + greeting + name at the
/// inline start; the bell (with the unread badge) and the menu at the end.
class GreetingHeader extends StatelessWidget {
  const GreetingHeader({
    required this.l10n,
    required this.name,
    super.key,
  });

  final AppL10n l10n;
  final String name;

  @override
  Widget build(BuildContext context) {
    // First name only (owner 2026-07-21) — the greeting shows just the given
    // name, not the full multi-part name. Name-less while the profile loads (or
    // for a name-less account) → just the wave, never a stray leading space.
    final firstName = name.trim().split(' ').first;
    final nameLine = firstName.isEmpty ? '👋' : '$firstName 👋';
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space2,
      ),
      child: Row(
        children: <Widget>[
          // Tapping the avatar opens the user's profile / My Area (owner
          // 2026-06-27). My Area IS the Profile tab, so go (switch tab) — not
          // push, which would stack a duplicate My Area that hangs on its own
          // dashboard load (owner-reported blank/frozen, 2026-07-11).
          Semantics(
            button: true,
            label: l10n.navProfile,
            child: InkWell(
              onTap: () {
                final shell = SimfShellScope.maybeOf(context);
                if (shell != null) {
                  shell.switchTab(tabIndex(SimfTab.profile));
                }
              },
              borderRadius:
                  const BorderRadius.all(Radius.circular(SimfTokens.radius)),
              child: SimfAvatar(name: name, currentUser: true),
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  l10n.greetingWelcome,
                  style: SimfTokens.bodyWhiteMd,
                ),
                const SizedBox(height: SimfTokens.gap2),
                Text(
                  nameLine,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: SimfTokens.labelGoldSemiboldLg,
                ),
              ],
            ),
          ),
          // The shared top-nav action cluster — identical to every sub-page:
          // the bell, the language globe, and the menu ☰, each a gold glyph in
          // a navy box. Home carries the unread badge.
          const SimfHeaderActions(showUnreadBadge: true),
        ],
      ),
    );
  }
}
