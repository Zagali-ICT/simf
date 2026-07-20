import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:go_router/go_router.dart';

import '../localization/app_l10n.dart';
import '../route_names.dart';
import '../theme/tokens.dart';
import 'simf_app_shell.dart' show SimfShellScope, tabIndex;
import 'simf_svg_icon.dart';

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
  const SimfBottomNav({super.key, required this.current});

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
    return Container(
      decoration: _barDecoration,
      child: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space2,
            vertical: SimfTokens.space2,
          ),
          child: SizedBox(
            height: 64,
            child: Row(
              children: <Widget>[
                _Item(
                  tab: SimfTab.home,
                  current: current,
                  iconAsset: 'assets/icons/nav_home.svg',
                  label: l10n.homeTitle,
                  onTap: _shellOrGo(context, SimfTab.home, RouteNames.home),
                ),
                _Item(
                  tab: SimfTab.sessions,
                  current: current,
                  iconAsset: 'assets/icons/nav_calendar.svg',
                  // D-750 (owner 2026-07-20) — the program tab now reads
                  // "الأجندة" (agendaTitle), reversing the earlier 206:1732
                  // "الجلسات" label. sessionsTitle still titles the Sessions
                  // screen and other surfaces, so only this nav tab changes.
                  label: l10n.agendaTitle,
                  onTap: _shellOrGo(context, SimfTab.sessions, RouteNames.sessions),
                ),
                _CentreAction(
                  active: current == SimfTab.badge,
                  label: l10n.badgeTitle,
                  onTap: _shellOrGo(context, SimfTab.badge, RouteNames.badge),
                ),
                _Item(
                  tab: SimfTab.map,
                  current: current,
                  iconAsset: 'assets/icons/nav_location.svg',
                  label: l10n.tileVenueMap,
                  onTap: _shellOrGo(context, SimfTab.map, RouteNames.venueMap),
                ),
                _Item(
                  tab: SimfTab.profile,
                  current: current,
                  iconAsset: 'assets/icons/nav_user.svg',
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

/// One destination: the exact iconify glyph (inactive `#5E584B`, active gold),
/// plus the gold label **below it only when active** (the KSA nav shows a single
/// label under the current tab; frame 758:1476).
class _Item extends StatelessWidget {
  const _Item({
    required this.tab,
    required this.current,
    required this.iconAsset,
    required this.label,
    required this.onTap,
  });

  final SimfTab tab;
  final SimfTab? current;
  final String iconAsset;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final active = tab == current;
    final color = active ? SimfTokens.goldSoft : SimfTokens.navInactive;
    return Expanded(
      child: Semantics(
        button: true,
        selected: active,
        label: label,
        child: InkWell(
          onTap: active ? null : onTap,
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              SimfSvgIcon(iconAsset, size: 24, color: color),
              if (active) ...<Widget>[
                const SizedBox(height: SimfTokens.space1),
                Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: SimfTokens.goldSoft,
                    fontSize: SimfTokens.textSm,
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

/// The raised gold QR centre action (frame 758:1476 "boxicons:qr", 56px) — the
/// bundled multi-colour SVG (gold disc, cream ring, navy glyph) rendered as-is.
class _CentreAction extends StatelessWidget {
  const _CentreAction({
    required this.active,
    required this.label,
    required this.onTap,
  });

  final bool active;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Center(
        child: Semantics(
          button: true,
          selected: active,
          label: label,
          child: InkWell(
            onTap: active ? null : onTap,
            customBorder: const CircleBorder(),
            child: SvgPicture.asset(
              'assets/icons/nav_qr.svg',
              width: 56,
              height: 56,
            ),
          ),
        ),
      ),
    );
  }
}
