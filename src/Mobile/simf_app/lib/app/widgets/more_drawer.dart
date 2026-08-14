import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/router.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/drawer_tile.dart';
import 'package:simf_app/app/widgets/simf_confirm_dialog.dart';
import 'package:simf_app/core/sharing/content_sharer.dart';
import 'package:simf_app/features/account/biometric_auth.dart';
import 'package:simf_app/features/more/more_menu_items.dart';
import 'package:simf_app/features/more/more_screen.dart' show MoreScreen;
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The shell's side drawer — the المزيد menu as a slide-in panel, opened by the
/// shared top bar's ☰ (in RTL it slides from the right). Holds the navigation
/// hub items (single source: [moreMenuEntries], shared with the full-page
/// [MoreScreen]) plus the account actions moved off the منطقتي page (D-396):
/// the Face-ID sign-in toggle, the calendar export and sign-out (the language
/// toggle + inert dark-mode tile were removed 2026-07-08 — language lives on
/// the More screen's settings row). The session-bound actions show only when
/// signed in.
class MoreDrawer extends ConsumerWidget {
  const MoreDrawer({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final auth = ref.watch(authControllerProvider);
    final signedIn = auth is AuthStateSignedIn;
    final user = signedIn ? auth.session.user : null;
    // A signed-in but not-yet-approved account presents as guest (D-666): the
    // attendee rows hide via the role gate, the approved-only rows (media
    // partners) via [MoreMenuEntry.approvedOnly], the account actions that need
    // an approved account (calendar) hide too, and it gets a single
    // "registration status" entry a true guest never sees.
    final role = user?.effectiveAppRole ?? AppRole.guest;
    final approved = user?.isApproved ?? false;
    final pending = signedIn && !approved;
    return Drawer(
      backgroundColor: SimfTokens.navySurface,
      child: SafeArea(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Padding(
              padding: const EdgeInsets.all(SimfTokens.space4),
              child: Text(
                // BUG-017 — was `moreTitle`, colliding with the Profile "More"
                // hub (a different menu with different entries).
                l10n.menuTitle,
                style: SimfTokens.labelWhiteSemiboldXl,
              ),
            ),
            const Divider(color: SimfTokens.beigeBorder, height: 1),
            Expanded(
              child: ListView(
                padding:
                    const EdgeInsets.symmetric(vertical: SimfTokens.space2),
                children: <Widget>[
                  // Navigation-hub entries, filtered to the role's own pages
                  // (D-519): unrestricted entries show for everyone; the
                  // attendee-only ones (rate, contacts) hide for
                  // Staff/Moderator and for an unapproved account (effective
                  // guest); the approved-only ones (media partners) hide for
                  // any non-approved account (D-666).
                  for (final entry in moreMenuEntries(l10n))
                    if (routeAllowsRole(entry.routeName, role) &&
                        (!entry.approvedOnly || approved) &&
                        (!entry.signedInOnly || signedIn))
                      DrawerTile(
                        icon: entry.icon,
                        title: entry.title,
                        onTap: () {
                          Navigator.of(context).pop();
                          unawaited(context.pushNamed(entry.routeName));
                        },
                      ),
                  // The one action a signed-in-but-unapproved account gets that
                  // a true guest does not: check its registration status
                  // (D-666).
                  if (pending)
                    DrawerTile(
                      icon: Icons.hourglass_top_outlined,
                      title: l10n.registrationStatusButton,
                      onTap: () {
                        Navigator.of(context).pop();
                        unawaited(context.pushNamed(RouteNames.registrationStatus));
                      },
                    ),
                  // Staff gate operations (D-406 / D-509) — Staff role only.
                  if (routeAllowsRole(RouteNames.gateScanner, role))
                    DrawerTile(
                      icon: Icons.qr_code_scanner,
                      title: l10n.gateScannerEntry,
                      onTap: () {
                        Navigator.of(context).pop();
                        unawaited(context.pushNamed(RouteNames.gateScanner));
                      },
                    ),
                  if (routeAllowsRole(RouteNames.staffRegisterVisitor, role))
                    DrawerTile(
                      icon: Icons.person_add_alt_1_outlined,
                      title: l10n.staffRegisterVisitorEntry,
                      onTap: () {
                        Navigator.of(context).pop();
                        unawaited(context.pushNamed(RouteNames.staffRegisterVisitor));
                      },
                    ),
                  // Exhibitor lead capture (D-426 / D-519) — Exhibitor role
                  // only (the JWT role drives it now, replacing the dashboard
                  // probe).
                  if (routeAllowsRole(RouteNames.scanVisitor, role))
                    DrawerTile(
                      icon: Icons.qr_code_scanner,
                      title: l10n.scanVisitorTitle,
                      onTap: () {
                        Navigator.of(context).pop();
                        unawaited(context.pushNamed(RouteNames.scanVisitor));
                      },
                    ),
                  if (routeAllowsRole(RouteNames.myVisitors, role))
                    DrawerTile(
                      icon: Icons.groups_outlined,
                      title: l10n.myVisitorsTitle,
                      onTap: () {
                        Navigator.of(context).pop();
                        unawaited(context.pushNamed(RouteNames.myVisitors));
                      },
                    ),
                  const Divider(color: SimfTokens.beigeBorder, height: 1),
                  // Account actions moved here from منطقتي (D-396). The
                  // language toggle + the inert dark-mode tile were removed
                  // (owner 2026-07-08): language is changed from the More
                  // screen's الإعدادات row (and the home header pill), and the
                  // app is navy-always (no light theme), so a dead dark-mode
                  // row added nothing to the menu. Face-ID sign-in toggle
                  // (D-441) — self-hides when the device has no usable
                  // biometric; enabling enrols a device key, disabling revokes
                  // it. Account action, so signed-in only.
                  if (signedIn) const FaceIdToggleTile(),
                  // Calendar export needs an approved account's schedule — hide
                  // it for a guest / not-yet-approved account (D-666).
                  if (approved)
                    DrawerTile(
                      icon: Icons.calendar_today_outlined,
                      title: l10n.shareCalendar,
                      onTap: () =>
                          unawaited(_shareCalendar(context, ref, l10n)),
                    ),
                  const Divider(color: SimfTokens.beigeBorder, height: 1),
                  // The end of the menu (owner 2026-07-06): contact us + about
                  // (app version / release date / organizer) + logout. Contact
                  // us and About are public — every account, incl. a guest —
                  // and About now carries the version (the old footer line is
                  // gone); logout is signed-in only.
                  DrawerTile(
                    icon: Icons.mail_outline,
                    title: l10n.contactUsTitle,
                    onTap: () {
                      Navigator.of(context).pop();
                      unawaited(context.pushNamed(RouteNames.contactUs));
                    },
                  ),
                  DrawerTile(
                    icon: Icons.info_outline,
                    title: l10n.aboutAppTitle,
                    onTap: () {
                      Navigator.of(context).pop();
                      unawaited(context.pushNamed(RouteNames.aboutApp));
                    },
                  ),
                  if (signedIn)
                    DrawerTile(
                      icon: Icons.logout,
                      title: l10n.signOutLink,
                      onTap: () =>
                          unawaited(_confirmSignOut(context, ref, l10n)),
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  /// Fetches the user's `.ics` and hands it to the native share sheet — the
  /// same export the profile page used to own.
  Future<void> _shareCalendar(
    BuildContext context,
    WidgetRef ref,
    AppL10n l10n,
  ) async {
    final messenger = ScaffoldMessenger.of(context);
    final repo = ref.read(myAreaRepositoryProvider);
    // Read the anchor rect BEFORE popping and awaiting — this element is gone
    // by the time the fetch returns, and the iPad share sheet must point at the
    // row the user actually tapped.
    final origin = shareOriginFromContext(context);
    Navigator.of(context).pop();
    try {
      final ics = await repo.getCalendarIcs();
      await shareTextContent(
        content: ics,
        filename: 'simf.ics',
        mimeType: 'text/calendar',
        sharePositionOrigin: origin,
      );
    } on ApiFailure {
      messenger.showSnackBar(SnackBar(content: Text(l10n.shareFailed)));
    }
  }

  /// Confirms, then revokes the session (D-373) and lands on sign-in. The
  /// captured router survives the await; context is not used after it.
  Future<void> _confirmSignOut(
    BuildContext context,
    WidgetRef ref,
    AppL10n l10n,
  ) async {
    final router = GoRouter.of(context);
    final auth = ref.read(authControllerProvider.notifier);
    final confirmed = await SimfConfirmDialog.show(
      context,
      title: l10n.signOutLink,
      message: l10n.signOutConfirmBody,
      confirmLabel: l10n.signOutLink,
      isDestructive: true,
    );
    if (!confirmed) {
      return;
    }
    await auth.signOut();
    router.goNamed(RouteNames.signIn);
  }
}
