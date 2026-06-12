import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../notifications/data/notifications_repository.dart';

/// LEGACY — the pre-redesign mockup Page 013 home, parked here when the KSA
/// Wave-2 home (frames 512:1492 / 203:1236) replaced it at `/`. Never routed;
/// kept compiling until the owner approves deleting the legacy directory at
/// programme close (§6 freeze rules).
///
/// A privilege-gated landing styled to `Mockup.html` screen 13: a "discover"
/// hero, the live banner, the feature grid, and the app's bottom navigation.
/// It opens for **everyone** (Guest included); the bell + profile avatar +
/// Visitor-only tiles are shaped by the **cached app privilege** (Guest when
/// signed out). Home carries no data of its own beyond the best-effort
/// unread-notification count for the bell badge (Page_013 L-5).
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final auth = ref.watch(authControllerProvider);
    final user = auth is AuthStateSignedIn ? auth.session.user : null;
    final isGuest = (user?.appRole ?? AppRole.guest) == AppRole.guest;
    // Best-effort: a guest or any wire error resolves to 0 (Logic L-5).
    final unread = ref.watch(unreadNotificationCountProvider).maybeWhen(
          data: (count) => count,
          orElse: () => 0,
        );

    return Scaffold(
      appBar: AppBar(
        automaticallyImplyLeading: false,
        leading: IconButton(
          tooltip: l10n.moreTitle,
          icon: const Icon(Icons.menu),
          onPressed: () => context.pushNamed(RouteNames.more),
        ),
        title: Text(l10n.homeTitle),
        actions: <Widget>[
          // The bell + badge and the profile avatar are for signed-in users
          // only; a guest has no personal notifications / area (Logic L-2).
          if (!isGuest)
            IconButton(
              tooltip: l10n.notificationsTooltip,
              onPressed: () => context.pushNamed(RouteNames.notifications),
              icon: Badge.count(
                count: unread,
                isLabelVisible: unread > 0,
                child: const Icon(Icons.notifications_outlined),
              ),
            ),
          if (!isGuest)
            Padding(
              padding: const EdgeInsetsDirectional.only(end: SimfTokens.space3),
              child: _ProfileAvatar(
                name: user?.displayName ?? '',
                onTap: () => context.pushNamed(RouteNames.myArea),
              ),
            ),
          if (isGuest) const SizedBox(width: SimfTokens.space2),
        ],
      ),
      bottomNavigationBar: const SimfBottomNav(current: SimfTab.home),
      body: SafeArea(
        top: false,
        child: ListView(
          padding: const EdgeInsets.all(SimfTokens.space4),
          children: <Widget>[
            _HeroCard(l10n: l10n),
            const SizedBox(height: SimfTokens.space3),
            _LiveBanner(
              l10n: l10n,
              onTap: () => context.pushNamed(RouteNames.liveBroadcast),
            ),
            if (isGuest) ...<Widget>[
              const SizedBox(height: SimfTokens.space3),
              _GuestPrompt(
                l10n: l10n,
                onSignIn: () => context.pushNamed(RouteNames.signIn),
              ),
            ],
            const SizedBox(height: SimfTokens.space5),
            _HomeTileGrid(l10n: l10n, isGuest: isGuest),
          ],
        ),
      ),
    );
  }
}

/// The "discover" hero (mockup `.dash-hero`).
class _HeroCard extends StatelessWidget {
  const _HeroCard({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Text(
              l10n.homeDiscoverTitle,
              style: const TextStyle(
                fontSize: SimfTokens.textXl,
                fontWeight: FontWeight.w700,
                color: SimfTokens.surface,
              ),
            ),
            const SizedBox(height: SimfTokens.space1),
            Text(
              l10n.homeDiscoverSubtitle,
              style: const TextStyle(
                color: SimfTokens.txtSecondary,
                fontSize: SimfTokens.textSm,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// A small gold profile avatar (mockup `.prof-av`) opening My-Area.
class _ProfileAvatar extends StatelessWidget {
  const _ProfileAvatar({required this.name, required this.onTap});

  final String name;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      customBorder: const CircleBorder(),
      child: Container(
        width: 30,
        height: 30,
        alignment: Alignment.center,
        decoration: const BoxDecoration(
          color: SimfTokens.accent,
          shape: BoxShape.circle,
        ),
        child: Text(
          _initials(name),
          style: const TextStyle(
            color: SimfTokens.navy,
            fontWeight: FontWeight.w700,
            fontSize: 11,
          ),
        ),
      ),
    );
  }

  String _initials(String name) {
    final parts = name
        .trim()
        .split(RegExp(r'\s+'))
        .where((p) => p.isNotEmpty)
        .toList();
    String first(String s) =>
        s.isEmpty ? '' : String.fromCharCode(s.runes.first).toUpperCase();
    if (parts.isEmpty) return '·';
    if (parts.length == 1) return first(parts.first);
    return first(parts.first) + first(parts.last);
  }
}

/// The live-broadcast promo banner (mockup home LIVE strip) — static / config
/// for now (no API, D10, Page_013 L-6); tapping it opens the live view.
class _LiveBanner extends StatelessWidget {
  const _LiveBanner({required this.l10n, required this.onTap});

  final AppL10n l10n;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
        child: Container(
          padding: const EdgeInsets.all(SimfTokens.space3),
          decoration: BoxDecoration(
            color: SimfTokens.danger.withValues(alpha: 0.06),
            border:
                Border.all(color: SimfTokens.danger.withValues(alpha: 0.45)),
            borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
          ),
          child: Row(
            children: <Widget>[
              Container(
                width: 34,
                height: 34,
                alignment: Alignment.center,
                decoration: const BoxDecoration(
                  color: SimfTokens.danger,
                  shape: BoxShape.circle,
                ),
                child: Text(
                  l10n.liveNowLabel,
                  style: const TextStyle(
                    color: SimfTokens.surface,
                    fontWeight: FontWeight.w700,
                    fontSize: 7.5,
                    letterSpacing: 0.5,
                  ),
                ),
              ),
              const SizedBox(width: SimfTokens.space3),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      l10n.liveBannerTitle,
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        color: SimfTokens.surface,
                        fontSize: SimfTokens.textSm,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      l10n.liveBannerSubtitle,
                      style: const TextStyle(
                        color: SimfTokens.txtSecondary,
                        fontSize: SimfTokens.textXs,
                      ),
                    ),
                  ],
                ),
              ),
              const Icon(Icons.chevron_left, color: SimfTokens.accent),
            ],
          ),
        ),
      ),
    );
  }
}

/// The guest sign-in prompt (Logic L-2) — shown only when signed out.
class _GuestPrompt extends StatelessWidget {
  const _GuestPrompt({required this.l10n, required this.onSignIn});

  final AppL10n l10n;
  final VoidCallback onSignIn;

  @override
  Widget build(BuildContext context) {
    return Card(
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        side: const BorderSide(color: SimfTokens.accent),
      ),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Text(
              l10n.guestPromptText,
              style: const TextStyle(color: SimfTokens.txtSecondary),
            ),
            const SizedBox(height: SimfTokens.space3),
            FilledButton(
              onPressed: onSignIn,
              child: Text(l10n.guestSignInCta),
            ),
          ],
        ),
      ),
    );
  }
}

/// The privilege-gated feature grid (mockup `.bento2` / `.row2` tiles). Public
/// tiles render for everyone; the Visitor+ tiles are appended when signed in.
class _HomeTileGrid extends StatelessWidget {
  const _HomeTileGrid({required this.l10n, required this.isGuest});

  final AppL10n l10n;
  final bool isGuest;

  @override
  Widget build(BuildContext context) {
    final tiles = <_TileSpec>[
      _TileSpec(l10n.tileSpeakers, Icons.groups_outlined, RouteNames.speakers),
      _TileSpec(l10n.tileBooths, Icons.storefront_outlined, RouteNames.booths),
      _TileSpec(
        l10n.tileSponsors,
        Icons.workspace_premium_outlined,
        RouteNames.sponsors,
      ),
      _TileSpec(l10n.tileNews, Icons.article_outlined, RouteNames.news),
      _TileSpec(l10n.tileArchive, Icons.bookmark_outline, RouteNames.archive),
      _TileSpec(l10n.tileAbout, Icons.info_outline, RouteNames.aboutForum),
      if (!isGuest) ...<_TileSpec>[
        _TileSpec(l10n.tileMyArea, Icons.person_outline, RouteNames.myArea),
        _TileSpec(
          l10n.tileEntryBadge,
          Icons.qr_code_2_outlined,
          RouteNames.badge,
        ),
        _TileSpec(
          l10n.tileMeetPeople,
          Icons.connect_without_contact_outlined,
          RouteNames.meetPeople,
        ),
      ],
    ];

    return GridView.count(
      crossAxisCount: 2,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      mainAxisSpacing: SimfTokens.space2,
      crossAxisSpacing: SimfTokens.space2,
      childAspectRatio: 1.6,
      children: <Widget>[
        for (final tile in tiles) _HomeTile(tile: tile),
      ],
    );
  }
}

/// One feature tile (mockup `.bt`): an icon over a bold label.
class _HomeTile extends StatelessWidget {
  const _HomeTile({required this.tile});

  final _TileSpec tile;

  @override
  Widget build(BuildContext context) {
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.pushNamed(tile.route),
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space3),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: <Widget>[
              Icon(tile.icon, color: SimfTokens.txtSecondary, size: 22),
              Text(
                tile.label,
                style: const TextStyle(
                  fontWeight: FontWeight.w600,
                  color: SimfTokens.surface,
                  fontSize: SimfTokens.textSm,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Immutable spec for one Home tile: its label, icon and destination route name.
class _TileSpec {
  const _TileSpec(this.label, this.icon, this.route);

  final String label;
  final IconData icon;
  final String route;
}
