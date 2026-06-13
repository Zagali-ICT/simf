import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../features/more/more_menu_items.dart';
import '../localization/app_l10n.dart';
import '../theme/tokens.dart';

/// The shell's side drawer — the المزيد menu as a slide-in panel, opened by the
/// shared top bar's ☰ (in RTL it slides from the right). Same items as the
/// full-page [MoreScreen] (single source: [moreMenuEntries]) in the navy KSA
/// styling. Tapping an item closes the drawer and pushes its route.
class MoreDrawer extends StatelessWidget {
  const MoreDrawer({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Drawer(
      backgroundColor: SimfTokens.navySurface,
      child: SafeArea(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Padding(
              padding: const EdgeInsets.all(SimfTokens.space4),
              child: Text(
                l10n.moreTitle,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: SimfTokens.textXl,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
            const Divider(color: SimfTokens.beigeBorder, height: 1),
            Expanded(
              child: ListView(
                padding:
                    const EdgeInsets.symmetric(vertical: SimfTokens.space2),
                children: <Widget>[
                  for (final entry in moreMenuEntries(l10n))
                    ListTile(
                      leading: Icon(entry.icon, color: SimfTokens.accent),
                      title: Text(
                        entry.title,
                        style: const TextStyle(color: Colors.white),
                      ),
                      onTap: () {
                        // Close the drawer first, then navigate.
                        Navigator.of(context).pop();
                        context.pushNamed(entry.routeName);
                      },
                    ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(SimfTokens.space4),
              child: Text(
                l10n.moreVersion,
                style: const TextStyle(
                  color: SimfTokens.inkMuted,
                  fontSize: SimfTokens.textSm,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
