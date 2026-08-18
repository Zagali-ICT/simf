import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/home/widgets/home_icons.dart';

/// The "عن الملتقى" group of the signed-in home (frames 758:1207 / 758:1215 /
/// 1052:12856 / 758:1228): the section bar, the 4-up about tiles, the
/// ask-a-moderator tile and the news tiles. Owns its trailing gap.
class HomeAboutSection extends StatelessWidget {
  const HomeAboutSection({
    required this.l10n,
    required this.canRequestMeetings,
    super.key,
  });

  final AppL10n l10n;

  /// Bi-Meeting rework — the "اللقاءات الثنائية" tile is shown to anyone
  /// entitled to request a meeting (speaker OR delegation flag); others don't
  /// see it (they can't reach the page).
  final bool canRequestMeetings;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        // "عن الملتقى" section bar (758:1207) — opens About the forum.
        SimfLinkRow(
          title: l10n.homeAboutSection,
          onTap: () => context.pushNamed(RouteNames.aboutForum),
        ),
        const SizedBox(height: SimfTokens.space6),
        // About tiles (frame 758:1215, h72) — a 4-up grid of the shared
        // tile, the same SimfNavTile reused as grid columns. Right→left
        // under RTL: المتحدثون · الأجنحة · الوفود · جلسات.
        SimfTileRow(
          children: <Widget>[
            SimfNavTile(
              label: l10n.tileSpeakers,
              iconAsset: HomeIcons.people,
              onTap: () => context.pushNamed(RouteNames.speakers),
            ),
            SimfNavTile(
              // Home button title matches the screen header ("المعرض").
              label: l10n.tileExhibition,
              iconAsset: HomeIcons.booths,
              onTap: () => context.pushNamed(RouteNames.booths),
            ),
            // الوفود — delegations sits in the about row (frame 758:1220)
            // with the design's exact formkit:people glyph (node
            // 1408:10399).
            SimfNavTile(
              label: l10n.delegationsTitle,
              iconAsset: HomeIcons.delegations,
              onTap: () => context.pushNamed(RouteNames.delegations),
            ),
            SimfNavTile(
              label: l10n.tileSessions,
              iconAsset: HomeIcons.aboutSessions,
              // Owner 2026-07-01: the home "الجلسات" tile opens the session
              // materials/downloads screen (Figma 1388:7621, header "الجلسات"),
              // whose title matches this label. The AI summaries list
              // ("ملخص الجلسات", 1388:8392) is the smart-features tile
              // below; the agenda lives on the bottom-nav sessions tab
              // (labelled "الجلسات" per nav component 206:1732 — the tab
              // label only shows when that tab is active, so Home renders
              // it icon-only).
              onTap: () => context.pushNamed(RouteNames.sessionPresentations),
            ),
          ],
        ),
        // 16px gap inside the "عن الملتقى" group (frame 1054:12864 gap-16).
        const SizedBox(height: SimfTokens.space4),
        // The full-width "اسأل المحاور" tile (1052:12856) — send a
        // question.
        SimfNavTile(
          label: l10n.tileAskModerator,
          iconAsset: HomeIcons.askModerator,
          onTap: () => context.pushNamed(RouteNames.sendQuestion),
        ),
        const SizedBox(height: SimfTokens.space6),
        // News tiles (758:1228, h80): right→left
        // اللقاءات الثنائية · الأرشيف.
        // D-745 — "اللقاءات الثنائية" now opens the bilateral-meetings
        // page ([RouteNames.meetings], Figma 1408:9726) and is hidden for
        // accounts without per-user meeting eligibility (D-760); the
        // requests history moved to My-Area. When hidden, الأرشيف fills
        // the row on its own (SimfTileRow expands each child).
        SimfTileRow(
          children: <Widget>[
            if (canRequestMeetings)
              SimfNavTile(
                label: l10n.tileBilateralMeetings,
                iconAsset: HomeIcons.bilateral,
                minHeight: SimfTokens.navTileHeight,
                onTap: () => context.pushNamed(RouteNames.meetings),
              ),
            SimfNavTile(
              label: l10n.tileArchive,
              iconAsset: HomeIcons.archive,
              minHeight: SimfTokens.navTileHeight,
              onTap: () => context.pushNamed(RouteNames.archive),
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space6),
      ],
    );
  }
}
