import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/home/widgets/discover_saudi_row.dart';
import 'package:simf_app/features/home/widgets/guest_banner.dart';
import 'package:simf_app/features/home/widgets/home_icons.dart';
import 'package:simf_app/features/home/widgets/pending_approval_card.dart';

/// Guest / unapproved layout (frame 758:2910 — "الرئيسية • ضيف", 2×2 tiles):
/// shown to a not-signed-in guest AND a signed-in but unapproved account.
class GuestHome extends StatelessWidget {
  const GuestHome(
      {required this.l10n, this.pendingApproval = false, super.key,});

  final AppL10n l10n;

  /// True when a signed-in but unapproved account is viewing this layout —
  /// shows the "awaiting approval" note instead of the sign-in CTA.
  final bool pendingApproval;

  @override
  Widget build(BuildContext context) {
    return SimfPageShell(
      title: l10n.homeGuestTitle,
      onBack: () => context.canPop()
          ? context.pop()
          : context.pushNamed(RouteNames.signIn),
      tab: SimfTab.home,
      showSweep: true,
      // The guest home is a Home variant (frame 758:2910), so it keeps the top
      // action cluster (language + menu) — opting back in over the new default
      // (sub-pages show back+title only). The bell is hidden: a guest has no
      // personal notifications.
      showHeaderActions: true,
      showNotificationsBell: false,
      body: ListView(
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          // The "browsing as guest, sign in" banner is for a NOT-signed-in
          // guest only. A signed-in but unapproved account is already logged in
          // (D-666) — it gets the under-review card at the bottom instead, never
          // a "please sign in" prompt.
          if (!pendingApproval) ...<Widget>[
            GuestBanner(l10n: l10n),
            const SizedBox(height: SimfTokens.space4),
          ],
          SimfTileRow(
            children: <Widget>[
              SimfNavTile(
                label: l10n.tileSessions,
                // Same icon as the signed-in "Sessions" tile (owner 2026-07-22).
                iconAsset: HomeIcons.aboutSessions,
                // Owner 2026-07-22: the "Sessions" tile opens the Sessions list
                // (session_presentations, header "الجلسات") — the SAME page the
                // signed-in "Sessions" tile opens — NOT the bottom-nav agenda.
                onTap: () => context.pushNamed(RouteNames.sessionPresentations),
              ),
              SimfNavTile(
                label: l10n.tileSpeakers,
                iconAsset: HomeIcons.speakers,
                onTap: () => context.pushNamed(RouteNames.speakers),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space2),
          SimfTileRow(
            children: <Widget>[
              SimfNavTile(
                label: l10n.tileVenueMap,
                iconAsset: HomeIcons.venueMap,
                onTap: () => context.pushNamed(RouteNames.venueMap),
              ),
              SimfNavTile(
                label: l10n.tileExhibition,
                iconAsset: HomeIcons.exhibition,
                onTap: () => context.pushNamed(RouteNames.booths),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space4),
          // The locked smart-badge card — a visual cue that signing in
          // unlocks it; never tappable as a guest. The hint is the only cue a
          // screen-reader user gets that it is locked and why (BUG-014).
          SimfNavTile(
            label: l10n.tileMyBadgeShort,
            iconAsset: HomeIcons.badge,
            enabled: false,
            disabledHint: l10n.guestBadgeLockedHint,
          ),
          const SizedBox(height: SimfTokens.space6),
          SimfSectionHeader(title: l10n.homeOpenInfoSection),
          const SizedBox(height: SimfTokens.space3),
          SimfListRow(
            title: l10n.faqRowTitle,
            subtitle: l10n.faqRowSubtitle,
            // Frame 758:2910 — the FAQ badge is the outlined (gold hairline)
            // box with a gold "?" glyph, not a solid gold fill.
            badgeOutlined: true,
            badge: const Icon(
              Icons.help_outline,
              size: SimfTokens.guestHomeSize,
              color: SimfTokens.accent,
            ),
            // Wave 1 added the public FAQ screen (GET /app/faq); the row opens
            // it directly now (was temporarily pointed at About before the
            // endpoint existed).
            onTap: () => context.pushNamed(RouteNames.faq),
          ),
          const SizedBox(height: SimfTokens.space4),
          DiscoverSaudiRow(l10n: l10n, outlined: true),
          const SizedBox(height: SimfTokens.space6),
          // True guest → the sign-in CTA; signed-in-but-unapproved → the
          // under-review card with re-check + registration-status actions (a
          // sign-in button would be wrong — they are already signed in, D-666).
          if (pendingApproval)
            PendingApprovalCard(l10n: l10n)
          else
            FilledButton(
              onPressed: () => context.pushNamed(RouteNames.signIn),
              child: Text(l10n.guestSignInCta),
            ),
        ],
      ),
    );
  }
}
