import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/centre_action.dart';
import 'package:simf_app/app/widgets/simf_app_shell.dart' show SimfAppShell, SimfShellScope, tabIndex;
import 'package:simf_app/app/widgets/simf_bottom_nav_item.dart';

/// When inside [SimfAppShell] (i.e., when [SimfShellScope] is the nearest
/// inherited ancestor), switch tabs via the shell. Outside the shell, navigate
/// via go_router.
VoidCallback _shellOrGo(BuildContext context, SimfTab tab, String routeName) {
  final shell = SimfShellScope.maybeOf(context);
  if (shell != null) {
    final index = tabIndex(tab);
    return () => shell.switchTab(index);
  }
  return () => context.goNamed(routeName);
}

/// The app's bottom navigation bar, rebuilt to the KSA-Project frame **758:1476
/// "Nav Bar"** (icons verified against the nav component **206:1699**): a navy
/// `#01132D` bar with rounded top corners, an upward gold glow, the exact
/// iconify glyphs (`vuesax/linear/user`, `tabler:map-pin-2` (the folded-map pin,
/// D-671 — replaced the old `boxicons:location` teardrop), `boxicons:qr`,
/// `uil:calender`, `vuesax/bold/home-2`) bundled under
/// `assets/icons/nav_*.svg`, a raised 56px gold QR centre action, inactive
/// icons in `#5E584B`, and the active tab in gold with its label below it.
/// Destinations (reading order): Home · Agenda · [QR badge] · Map · Profile —
/// the Profile tab replaced the old News tab per the delivered frames. Tapping
/// a destination navigates via go_router; the active tab is a no-op. [current]
/// is null on pages that keep the bar but are not a destination themselves.
enum SimfTab { home, sessions, badge, map, profile }

class SimfBottomNav extends StatelessWidget {
  const SimfBottomNav({required this.current, super.key});

  final SimfTab? current;

  // The bar never changes shape — built once, not per host-page rebuild.
  static final BoxDecoration _barDecoration = BoxDecoration(
    color: SimfTokens.navy,
    borderRadius: const BorderRadius.vertical(
      top: Radius.circular(SimfTokens.radiusLg),
    ),
    boxShadow: <BoxShadow>[
      BoxShadow(
        color: SimfTokens.goldSoft.withValues(alpha: 0.16),
        offset: const Offset(0, -1),
        blurRadius: 6,
      ),
    ],
  );

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return DecoratedBox(
      decoration: _barDecoration,
      child: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space2,
            vertical: SimfTokens.space2,
          ),
          child: SizedBox(
            height: SimfTokens.simfBottomNavHeight,
            child: Row(
              children: <Widget>[
                SimfBottomNavItem(
                  tab: SimfTab.home,
                  current: current,
                  iconAsset: AppAssets.navHome,
                  label: l10n.homeTitle,
                  onTap: _shellOrGo(context, SimfTab.home, RouteNames.home),
                ),
                SimfBottomNavItem(
                  tab: SimfTab.sessions,
                  current: current,
                  iconAsset: AppAssets.navCalendar,
                  // D-750 (owner 2026-07-20) — the program tab now reads
                  // "الأجندة" (agendaTitle), reversing the earlier 206:1732
                  // "الجلسات" label. sessionsTitle still titles the Sessions
                  // screen and other surfaces, so only this nav tab changes.
                  label: l10n.agendaTitle,
                  onTap: _shellOrGo(context, SimfTab.sessions, RouteNames.sessions),
                ),
                CentreAction(
                  active: current == SimfTab.badge,
                  label: l10n.badgeTitle,
                  onTap: _shellOrGo(context, SimfTab.badge, RouteNames.badge),
                ),
                SimfBottomNavItem(
                  tab: SimfTab.map,
                  current: current,
                  iconAsset: AppAssets.navLocation,
                  label: l10n.tileVenueMap,
                  onTap: _shellOrGo(context, SimfTab.map, RouteNames.venueMap),
                ),
                SimfBottomNavItem(
                  tab: SimfTab.profile,
                  current: current,
                  iconAsset: AppAssets.navUser,
                  label: l10n.navProfile,
                  onTap: _shellOrGo(context, SimfTab.profile, RouteNames.myArea),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
