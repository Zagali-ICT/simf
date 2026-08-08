import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_app_shell.dart' show SimfShellScope, tabIndex;
import 'package:simf_app/app/widgets/simf_bottom_nav.dart' show SimfTab;
import 'package:simf_app/app/widgets/simf_language_toggle.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';

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
    // Greet the FULL trimmed name (OA-D1). The old "first token only" split
    // (owner 2026-07-21) broke every Arabic compound given name — عبد الله
    // greeted as عبد, عبد الرحمن as عبد — and greeted the wrong name whenever
    // the family name came first. There is no captured GivenName to split on,
    // so no amount of string surgery is safe; the Text below already carries
    // maxLines: 1 + TextOverflow.ellipsis, so a long name degrades gracefully.
    // Name-less while the profile loads (or for a name-less account) → just the
    // wave, never a stray leading space.
    final trimmedName = name.trim();
    final nameLine = trimmedName.isEmpty ? '👋' : '$trimmedName 👋';
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
          // The shared language toggle. Every other screen carries it in its
          // header, but the signed-in Home did not — and the language row lives
          // only in the Profile "More" menu, so from Home there was no route to
          // the language switch at all (BUG-017). It sits BESIDE the action
          // cluster, not inside it: the pill was dropped from the shared
          // cluster on 2026-07-11 and the owner reversed that for Home only on
          // 2026-07-27 ("keep home lang", D-772). Keep it here.
          Consumer(
            builder: (context, ref, _) => SimfLanguageToggle(
              onPressed: () => unawaited(
                ref.read(localeControllerProvider.notifier).toggle(),
              ),
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          // The shared top-nav action cluster — identical to every sub-page:
          // the bell and the menu ☰, each a gold glyph in a navy box. Home
          // carries the unread badge.
          const SimfHeaderActions(showUnreadBadge: true),
        ],
      ),
    );
  }
}
