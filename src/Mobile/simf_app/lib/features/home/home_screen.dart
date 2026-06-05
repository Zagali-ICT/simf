import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../notifications/data/notifications_repository.dart';

/// Page 013 — الرئيسية · Home (router / landing screen #13, `path=/`).
///
/// A privilege-gated landing: it opens for **everyone** (Guest included, no
/// login) and the visible tiles + the notification bell are shaped by the
/// **cached app privilege** read from the auth state (Guest when signed out —
/// Page_013 L-1/L-2). Home carries **no data of its own**; its one live call is
/// the best-effort unread-notification count for the bell badge (L-5). The live
/// banner is static for now (no API — D10).
///
/// The tile inventory here is the **interim** functional set wired to the
/// existing routes (the final per-privilege catalogue is owner-driven and not
/// finalized — Page_013 L-2); the final visuals come from the designer
/// (SIMF-VID-001).
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final auth = ref.watch(authControllerProvider);
    final role =
        auth is AuthStateSignedIn ? auth.session.user.appRole : AppRole.guest;
    final isGuest = role == AppRole.guest;
    // Best-effort: a guest or any wire error resolves to 0 (Logic L-5).
    final unread = ref.watch(unreadNotificationCountProvider).maybeWhen(
          data: (count) => count,
          orElse: () => 0,
        );

    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.homeTitle),
        automaticallyImplyLeading: false,
        actions: <Widget>[
          // The bell + badge is for signed-in users only; a guest has no
          // personal notifications (Logic L-2).
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
          const SizedBox(width: SimfTokens.space2),
        ],
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(SimfTokens.space4),
          children: <Widget>[
            _DiscoverHeader(l10n: l10n),
            const SizedBox(height: SimfTokens.space4),
            _LiveBanner(
              l10n: l10n,
              onTap: () => context.pushNamed(RouteNames.liveBroadcast),
            ),
            if (isGuest) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
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

/// The "Discover" landing header (interim copy standing in for the mockup hero).
class _DiscoverHeader extends StatelessWidget {
  const _DiscoverHeader({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          l10n.homeDiscoverTitle,
          style: const TextStyle(
            fontSize: SimfTokens.textXl,
            fontWeight: FontWeight.w700,
          ),
        ),
        const SizedBox(height: SimfTokens.space1),
        Text(
          l10n.homeDiscoverSubtitle,
          style: const TextStyle(color: SimfTokens.inkMuted),
        ),
      ],
    );
  }
}

/// The live-broadcast promo banner. Static / config-driven for now — no API
/// (D10, Page_013 L-6); tapping it opens the live view.
class _LiveBanner extends StatelessWidget {
  const _LiveBanner({required this.l10n, required this.onTap});

  final AppL10n l10n;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space4),
          child: Row(
            children: <Widget>[
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: SimfTokens.space2,
                  vertical: SimfTokens.space1,
                ),
                decoration: BoxDecoration(
                  color: SimfTokens.danger,
                  borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                ),
                child: Text(
                  l10n.liveNowLabel,
                  style: const TextStyle(
                    color: SimfTokens.surface,
                    fontWeight: FontWeight.w700,
                    fontSize: SimfTokens.textXs,
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
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                    const SizedBox(height: SimfTokens.space1),
                    Text(
                      l10n.liveBannerSubtitle,
                      style: const TextStyle(
                        color: SimfTokens.inkMuted,
                        fontSize: SimfTokens.textSm,
                      ),
                    ),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right, color: SimfTokens.accent),
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
      margin: EdgeInsets.zero,
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
              style: const TextStyle(color: SimfTokens.inkMuted),
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

/// The privilege-gated tile grid. Public tiles render for everyone; the
/// Visitor+ tiles are appended only when signed in (Logic L-2).
class _HomeTileGrid extends StatelessWidget {
  const _HomeTileGrid({required this.l10n, required this.isGuest});

  final AppL10n l10n;
  final bool isGuest;

  @override
  Widget build(BuildContext context) {
    final tiles = <_TileSpec>[
      _TileSpec(l10n.tileSessions, Icons.event_note_outlined, RouteNames.sessions),
      _TileSpec(l10n.tileSpeakers, Icons.groups_outlined, RouteNames.speakers),
      _TileSpec(l10n.tileVenueMap, Icons.map_outlined, RouteNames.venueMap),
      _TileSpec(l10n.tileBooths, Icons.storefront_outlined, RouteNames.booths),
      _TileSpec(l10n.tileSponsors, Icons.workspace_premium_outlined, RouteNames.sponsors),
      _TileSpec(l10n.tileNews, Icons.article_outlined, RouteNames.news),
      _TileSpec(l10n.tileArchive, Icons.bookmark_outline, RouteNames.archive),
      _TileSpec(l10n.tileAbout, Icons.info_outline, RouteNames.aboutForum),
      if (!isGuest) ...<_TileSpec>[
        _TileSpec(l10n.tileMyArea, Icons.person_outline, RouteNames.myArea),
        _TileSpec(l10n.tileEntryBadge, Icons.qr_code_2_outlined, RouteNames.badge),
        _TileSpec(l10n.tileMeetPeople, Icons.connect_without_contact_outlined, RouteNames.meetPeople),
      ],
    ];

    return GridView.count(
      crossAxisCount: 2,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      mainAxisSpacing: SimfTokens.space3,
      crossAxisSpacing: SimfTokens.space3,
      childAspectRatio: 1.5,
      children: <Widget>[
        for (final tile in tiles) _HomeTile(tile: tile),
      ],
    );
  }
}

/// One navigation tile (icon + label) on the Home grid.
class _HomeTile extends StatelessWidget {
  const _HomeTile({required this.tile});

  final _TileSpec tile;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.pushNamed(tile.route),
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space3),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: <Widget>[
              Icon(tile.icon, color: SimfTokens.accent),
              Text(
                tile.label,
                style: const TextStyle(fontWeight: FontWeight.w600),
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
