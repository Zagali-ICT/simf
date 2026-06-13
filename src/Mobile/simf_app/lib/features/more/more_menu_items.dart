import 'package:flutter/material.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';

/// One entry in the المزيد menu: an icon, the localized title, and the route it
/// opens. Single source of truth shared by the full-page [MoreScreen] and the
/// shell's side drawer (`MoreDrawer`) so the list never drifts between them.
@immutable
class MoreMenuEntry {
  const MoreMenuEntry({
    required this.icon,
    required this.title,
    required this.routeName,
  });

  final IconData icon;
  final String title;
  final String routeName;
}

/// The navigation hub items (About → Media partners), in display order.
List<MoreMenuEntry> moreMenuEntries(AppL10n l10n) => <MoreMenuEntry>[
      MoreMenuEntry(
        icon: Icons.info_outline,
        title: l10n.moreAbout,
        routeName: RouteNames.aboutForum,
      ),
      MoreMenuEntry(
        icon: Icons.accessibility_new_outlined,
        title: l10n.moreAccessibility,
        routeName: RouteNames.accessibility,
      ),
      MoreMenuEntry(
        icon: Icons.gavel_outlined,
        title: l10n.moreTerms,
        routeName: RouteNames.terms,
      ),
      MoreMenuEntry(
        icon: Icons.star_outline,
        title: l10n.moreRate,
        routeName: RouteNames.rate,
      ),
      MoreMenuEntry(
        icon: Icons.notifications_outlined,
        title: l10n.moreNotifications,
        routeName: RouteNames.notifications,
      ),
      MoreMenuEntry(
        icon: Icons.qr_code_2_outlined,
        title: l10n.shareMyContactTitle,
        routeName: RouteNames.shareMyContact,
      ),
      MoreMenuEntry(
        icon: Icons.contacts_outlined,
        title: l10n.myContactsTitle,
        routeName: RouteNames.myContacts,
      ),
      MoreMenuEntry(
        icon: Icons.handshake_outlined,
        title: l10n.moreMediaPartners,
        routeName: RouteNames.mediaPartners,
      ),
    ];
