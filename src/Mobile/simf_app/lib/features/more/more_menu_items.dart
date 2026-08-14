import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/features/more/more_screen.dart' show MoreScreen;

/// One entry in the المزيد menu: an icon, the localized title, and the route it
/// opens. Single source of truth shared by the full-page [MoreScreen] and the
/// shell's side drawer (`MoreDrawer`) so the list never drifts between them.
@immutable
class MoreMenuEntry {
  const MoreMenuEntry({
    required this.icon,
    required this.title,
    required this.routeName,
    this.approvedOnly = false,
    this.signedInOnly = false,
  });

  final IconData icon;
  final String title;
  final String routeName;

  /// When true the entry is advertised only to an **approved** account — the
  /// target page stays reachable elsewhere (e.g. media partners from the public
  /// News/Gallery coverage tabs), but a guest / not-yet-approved account does
  /// not see it in the menu (D-666). Route-role-gated entries (rate, contacts)
  /// don't need this flag — `routeAllowsRole` already hides them from a guest.
  final bool approvedOnly;

  /// When true the entry is shown only to a **signed-in** account — an
  /// auth-required page (e.g. notifications) that a not-logged-in guest cannot
  /// use, so it should not appear in the menu and dead-bounce to sign-in
  /// (D-669).
  final bool signedInOnly;
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
        signedInOnly: true,
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
        approvedOnly: true,
      ),
    ];
