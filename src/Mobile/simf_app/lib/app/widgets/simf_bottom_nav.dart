import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../localization/app_l10n.dart';
import '../route_names.dart';
import '../theme/tokens.dart';

/// The app's bottom navigation bar, rebuilt to the KSA-Project Wave-2 frames
/// (e.g. 512:1492 "Visitor-Home-2" nav component 206:1669): a navy bar with
/// rounded top corners, an upward gold glow, a raised 56px gold QR centre
/// action, and **only the active tab** showing its label. Destinations (in
/// reading order): Home · Agenda · [QR badge] · Map · Profile — the Profile
/// tab replaced the old News tab per the delivered frames (owner-approved,
/// W2 batch). Tapping a destination navigates via go_router; the active tab
/// is a no-op. [current] is null on pages that keep the bar but are not a
/// destination themselves (e.g. News).
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
                  icon: Icons.home_rounded,
                  label: l10n.homeTitle,
                  onTap: () => context.goNamed(RouteNames.home),
                ),
                _Item(
                  tab: SimfTab.sessions,
                  current: current,
                  icon: Icons.calendar_today_outlined,
                  label: l10n.navAgenda,
                  onTap: () => _push(context, RouteNames.sessions),
                ),
                _CentreAction(
                  active: current == SimfTab.badge,
                  onTap: () => _push(context, RouteNames.badge),
                ),
                _Item(
                  tab: SimfTab.map,
                  current: current,
                  icon: Icons.location_on_outlined,
                  label: l10n.tileVenueMap,
                  onTap: () => _push(context, RouteNames.venueMap),
                ),
                _Item(
                  tab: SimfTab.profile,
                  current: current,
                  icon: Icons.person_outline,
                  label: l10n.navProfile,
                  onTap: () => _push(context, RouteNames.myArea),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  void _push(BuildContext context, String route) {
    context.pushNamed(route);
  }
}

/// One destination: the icon, plus the label **below it only when active**
/// (the KSA nav shows a single gold label under the current tab).
class _Item extends StatelessWidget {
  const _Item({
    required this.tab,
    required this.current,
    required this.icon,
    required this.label,
    required this.onTap,
  });

  final SimfTab tab;
  final SimfTab? current;
  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final active = tab == current;
    final color = active ? SimfTokens.accent : SimfTokens.txtSecondary;
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
              Icon(icon, color: color, size: 24),
              if (active) ...<Widget>[
                const SizedBox(height: SimfTokens.space1),
                Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: SimfTokens.accent,
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

/// The raised gold QR centre action (frame component "boxicons:qr", 56px).
class _CentreAction extends StatelessWidget {
  const _CentreAction({required this.active, required this.onTap});

  final bool active;
  final VoidCallback onTap;

  static final BoxDecoration _decoration = BoxDecoration(
    color: SimfTokens.accent,
    shape: BoxShape.circle,
    border: Border.all(color: SimfTokens.navy, width: 3),
    boxShadow: <BoxShadow>[
      BoxShadow(
        color: SimfTokens.goldSoft.withValues(alpha: 0.35),
        blurRadius: 10,
      ),
    ],
  );

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Center(
        child: InkWell(
          onTap: active ? null : onTap,
          customBorder: const CircleBorder(),
          child: Container(
            width: 56,
            height: 56,
            decoration: _decoration,
            child: const Icon(
              Icons.qr_code_2_rounded,
              color: SimfTokens.navy,
              size: 28,
            ),
          ),
        ),
      ),
    );
  }
}
