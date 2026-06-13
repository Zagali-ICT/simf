import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import 'more_menu_items.dart';

/// Page 041 — المزيد · More (#41, `/more`, public).
///
/// **Public — no API.** A navigation hub: a list of tiles that route to the
/// already-built secondary screens (About, Accessibility, Terms, Rate,
/// Notifications, Share my contact, My Contacts, Media partners), with a static
/// app-version line at the bottom. The same items also back the shell's side
/// drawer (`MoreDrawer`) via the shared [moreMenuEntries] list.
class MoreScreen extends StatelessWidget {
  const MoreScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l10n.moreTitle)),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.symmetric(vertical: SimfTokens.space2),
          children: <Widget>[
            for (final entry in moreMenuEntries(l10n))
              _MoreTile(
                icon: entry.icon,
                title: entry.title,
                routeName: entry.routeName,
              ),
            const SizedBox(height: SimfTokens.space4),
            Padding(
              padding: const EdgeInsets.all(SimfTokens.space4),
              child: Center(
                child: Text(
                  l10n.moreVersion,
                  style: const TextStyle(
                    color: SimfTokens.inkMuted,
                    fontSize: SimfTokens.textSm,
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MoreTile extends StatelessWidget {
  const _MoreTile({
    required this.icon,
    required this.title,
    required this.routeName,
  });

  final IconData icon;
  final String title;
  final String routeName;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      leading: Icon(icon, color: SimfTokens.accent),
      title: Text(title),
      trailing: const Icon(
        Icons.chevron_right,
        color: SimfTokens.inkMuted,
      ),
      onTap: () => context.pushNamed(routeName),
    );
  }
}
