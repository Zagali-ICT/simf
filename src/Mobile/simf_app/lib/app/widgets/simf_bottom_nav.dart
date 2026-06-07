import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../localization/app_l10n.dart';
import '../route_names.dart';
import '../theme/tokens.dart';

/// The app's bottom navigation bar, matching `Mockup.html` `.bottom-nav`:
/// Home · Sessions · [centre badge action] · Map · Media — a navy bar with a
/// raised gold centre. Tapping a destination navigates via go_router; the
/// active tab is a no-op. Used by the primary screens (Home, Sessions, …).
enum SimfTab { home, sessions, badge, map, media }

class SimfBottomNav extends StatelessWidget {
  const SimfBottomNav({super.key, required this.current});

  final SimfTab current;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Container(
      decoration: const BoxDecoration(
        color: SimfTokens.navy,
        border: Border(top: BorderSide(color: SimfTokens.line2)),
      ),
      child: SafeArea(
        top: false,
        child: SizedBox(
          height: 62,
          child: Row(
            children: <Widget>[
              _Item(
                tab: SimfTab.home,
                current: current,
                icon: Icons.home_outlined,
                label: l10n.homeTitle,
                onTap: () => context.goNamed(RouteNames.home),
              ),
              _Item(
                tab: SimfTab.sessions,
                current: current,
                icon: Icons.event_note_outlined,
                label: l10n.tileSessions,
                onTap: () => _push(context, RouteNames.sessions),
              ),
              _CentreAction(
                active: current == SimfTab.badge,
                onTap: () => _push(context, RouteNames.badge),
              ),
              _Item(
                tab: SimfTab.map,
                current: current,
                icon: Icons.place_outlined,
                label: l10n.tileVenueMap,
                onTap: () => _push(context, RouteNames.venueMap),
              ),
              _Item(
                tab: SimfTab.media,
                current: current,
                icon: Icons.grid_view_outlined,
                label: l10n.tileNews,
                onTap: () => _push(context, RouteNames.news),
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _push(BuildContext context, String route) {
    context.pushNamed(route);
  }
}

class _Item extends StatelessWidget {
  const _Item({
    required this.tab,
    required this.current,
    required this.icon,
    required this.label,
    required this.onTap,
  });

  final SimfTab tab;
  final SimfTab current;
  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final active = tab == current;
    final color = active ? SimfTokens.accent : SimfTokens.txtTertiary;
    return Expanded(
      child: InkWell(
        onTap: active ? null : onTap,
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Icon(icon, color: color, size: 22),
            const SizedBox(height: 3),
            Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                color: color,
                fontSize: 9.5,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The raised gold centre action (mockup `.bn-i.center`) — the entry-badge QR.
class _CentreAction extends StatelessWidget {
  const _CentreAction({required this.active, required this.onTap});

  final bool active;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Center(
        child: InkWell(
          onTap: active ? null : onTap,
          customBorder: const CircleBorder(),
          child: Container(
            width: 46,
            height: 46,
            decoration: BoxDecoration(
              color: SimfTokens.accent,
              shape: BoxShape.circle,
              border: Border.all(color: SimfTokens.navy, width: 3),
            ),
            child: const Icon(
              Icons.qr_code_2_rounded,
              color: SimfTokens.navy,
              size: 24,
            ),
          ),
        ),
      ),
    );
  }
}
