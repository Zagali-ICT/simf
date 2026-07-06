import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../core/sharing/content_sharer.dart';
import '../../features/account/biometric_auth.dart';
import '../../features/more/more_menu_items.dart';
import '../../features/myarea/data/myarea_repository.dart';
import '../localization/app_l10n.dart';
import '../localization/locale_controller.dart';
import '../route_names.dart';
import '../router.dart';
import '../theme/tokens.dart';
import 'simf_confirm_dialog.dart';

/// The shell's side drawer — the المزيد menu as a slide-in panel, opened by the
/// shared top bar's ☰ (in RTL it slides from the right). Holds the navigation
/// hub items (single source: [moreMenuEntries], shared with the full-page
/// [MoreScreen]) plus the account actions moved off the منطقتي page (D-396):
/// the language toggle, the (inert) theme control, the calendar export and
/// sign-out. The session-bound actions show only when signed in.
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
                  // Navigation-hub entries, filtered to the role's own pages
                  // (D-519): unrestricted entries show for everyone; the
                  // attendee-only ones (rate, contacts) hide for Staff/Moderator
                  // and for an unapproved account (effective guest); the
                  // approved-only ones (media partners) hide for any non-approved
                  // account (D-666).
                  for (final entry in moreMenuEntries(l10n))
                    if (routeAllowsRole(entry.routeName, role) &&
                        (!entry.approvedOnly || approved) &&
                        (!entry.signedInOnly || signedIn))
                      _DrawerTile(
                        icon: entry.icon,
                        title: entry.title,
                        onTap: () {
                          Navigator.of(context).pop();
                          context.pushNamed(entry.routeName);
                        },
                      ),
                  // The one action a signed-in-but-unapproved account gets that a
                  // true guest does not: check its registration status (D-666).
                  if (pending)
                    _DrawerTile(
                      icon: Icons.hourglass_top_outlined,
                      title: l10n.registrationStatusButton,
                      onTap: () {
                        Navigator.of(context).pop();
                        context.pushNamed(RouteNames.registrationStatus);
                      },
                    ),
                  // Staff gate operations (D-406 / D-509) — Staff role only.
                  if (routeAllowsRole(RouteNames.gateScanner, role))
                    _DrawerTile(
                      icon: Icons.qr_code_scanner,
                      title: l10n.gateScannerEntry,
                      onTap: () {
                        Navigator.of(context).pop();
                        context.pushNamed(RouteNames.gateScanner);
                      },
                    ),
                  if (routeAllowsRole(RouteNames.staffRegisterVisitor, role))
                    _DrawerTile(
                      icon: Icons.person_add_alt_1_outlined,
                      title: l10n.staffRegisterVisitorEntry,
                      onTap: () {
                        Navigator.of(context).pop();
                        context.pushNamed(RouteNames.staffRegisterVisitor);
                      },
                    ),
                  // Exhibitor lead capture (D-426 / D-519) — Exhibitor role only
                  // (the JWT role drives it now, replacing the dashboard probe).
                  if (routeAllowsRole(RouteNames.scanVisitor, role))
                    _DrawerTile(
                      icon: Icons.qr_code_scanner,
                      title: l10n.scanVisitorTitle,
                      onTap: () {
                        Navigator.of(context).pop();
                        context.pushNamed(RouteNames.scanVisitor);
                      },
                    ),
                  if (routeAllowsRole(RouteNames.myVisitors, role))
                    _DrawerTile(
                      icon: Icons.groups_outlined,
                      title: l10n.myVisitorsTitle,
                      onTap: () {
                        Navigator.of(context).pop();
                        context.pushNamed(RouteNames.myVisitors);
                      },
                    ),
                  const Divider(color: SimfTokens.beigeBorder, height: 1),
                  // Account actions moved here from منطقتي (D-396).
                  _DrawerTile(
                    icon: Icons.language,
                    title: l10n.languageToggleLabel,
                    onTap: () {
                      Navigator.of(context).pop();
                      unawaited(
                        ref.read(localeControllerProvider.notifier).toggle(),
                      );
                    },
                  ),
                  // Visible but inert — the app has no light theme yet (owner
                  // decision, carried over from the profile tile).
                  _DrawerTile(
                    icon: Icons.dark_mode_outlined,
                    title: l10n.themeToggleTooltip,
                    enabled: false,
                  ),
                  // Face-ID sign-in toggle (D-441) — self-hides when the device
                  // has no usable biometric; enabling enrols a device key,
                  // disabling revokes it. Account action, so signed-in only.
                  if (signedIn) const FaceIdToggleTile(),
                  // Calendar export needs an approved account's schedule — hide
                  // it for a guest / not-yet-approved account (D-666).
                  if (approved)
                    _DrawerTile(
                      icon: Icons.calendar_today_outlined,
                      title: l10n.shareCalendar,
                      onTap: () => unawaited(_shareCalendar(context, ref, l10n)),
                    ),
                  const Divider(color: SimfTokens.beigeBorder, height: 1),
                  // The end of the menu (owner 2026-07-06): contact us + about
                  // (app version / release date / organizer) + logout. Contact
                  // us and About are public — every account, incl. a guest — and
                  // About now carries the version (the old footer line is gone);
                  // logout is signed-in only.
                  _DrawerTile(
                    icon: Icons.mail_outline,
                    title: l10n.contactUsTitle,
                    onTap: () {
                      Navigator.of(context).pop();
                      context.pushNamed(RouteNames.contactUs);
                    },
                  ),
                  _DrawerTile(
                    icon: Icons.info_outline,
                    title: l10n.aboutAppTitle,
                    onTap: () {
                      Navigator.of(context).pop();
                      context.pushNamed(RouteNames.aboutApp);
                    },
                  ),
                  if (signedIn)
                    _DrawerTile(
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
    Navigator.of(context).pop();
    try {
      final ics = await repo.getCalendarIcs();
      await shareTextContent(
        content: ics,
        filename: 'simf.ics',
        mimeType: 'text/calendar',
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

/// One drawer row in the navy KSA styling: a gold leading icon over a white
/// title. [enabled] false renders the muted, non-tappable variant.
class _DrawerTile extends StatelessWidget {
  const _DrawerTile({
    required this.icon,
    required this.title,
    this.onTap,
    this.enabled = true,
  });

  final IconData icon;
  final String title;
  final VoidCallback? onTap;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    final color = enabled ? SimfTokens.accent : SimfTokens.inkMuted;
    final titleColor = enabled ? Colors.white : SimfTokens.inkMuted;
    return ListTile(
      enabled: enabled,
      leading: Icon(icon, color: color),
      title: Text(title, style: TextStyle(color: titleColor)),
      onTap: onTap,
    );
  }
}

