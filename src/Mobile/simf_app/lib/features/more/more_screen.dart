import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';

/// Page 041 — المزيد · More (#41, `/more`, public).
///
/// **Public — no API.** A navigation hub: a list of tiles that route to the
/// already-built secondary screens (About, Accessibility, Terms, Rate,
/// Notifications, Media partners), with a static app-version line at the
/// bottom. UI is interim (final visuals from SIMF-VID-001).
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
            _MoreTile(
              icon: Icons.info_outline,
              title: l10n.moreAbout,
              routeName: RouteNames.aboutForum,
            ),
            _MoreTile(
              icon: Icons.accessibility_new_outlined,
              title: l10n.moreAccessibility,
              routeName: RouteNames.accessibility,
            ),
            _MoreTile(
              icon: Icons.gavel_outlined,
              title: l10n.moreTerms,
              routeName: RouteNames.terms,
            ),
            _MoreTile(
              icon: Icons.star_outline,
              title: l10n.moreRate,
              routeName: RouteNames.rate,
            ),
            _MoreTile(
              icon: Icons.notifications_outlined,
              title: l10n.moreNotifications,
              routeName: RouteNames.notifications,
            ),
            _MoreTile(
              icon: Icons.qr_code_2_outlined,
              title: l10n.shareMyContactTitle,
              routeName: RouteNames.shareMyContact,
            ),
            _MoreTile(
              icon: Icons.contacts_outlined,
              title: l10n.myContactsTitle,
              routeName: RouteNames.myContacts,
            ),
            _MoreTile(
              icon: Icons.handshake_outlined,
              title: l10n.moreMediaPartners,
              routeName: RouteNames.mediaPartners,
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
