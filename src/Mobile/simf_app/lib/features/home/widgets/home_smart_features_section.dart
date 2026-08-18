import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/home/widgets/home_icons.dart';

/// "الميزات الذكية" (758:1158) — the smart-features header + its two tile
/// rows. Owns its trailing gap.
class HomeSmartFeaturesSection extends StatelessWidget {
  const HomeSmartFeaturesSection({
    required this.l10n,
    required this.partnerDirectoryEnabled,
    super.key,
  });

  final AppL10n l10n;

  /// Build #13 — the "قابل أشخاص مثلك" tile is hidden when the CP switch for
  /// the partner directory is off (the feature is unavailable).
  final bool partnerDirectoryEnabled;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        // "الميزات الذكية" (758:1158) — header + the المزيد link → More.
        SimfSectionHeader(
          title: l10n.homeSmartSection,
          moreLabel: l10n.moreTitle,
          onMore: () => context.pushNamed(RouteNames.more),
        ),
        const SizedBox(height: SimfTokens.space4),
        SimfTileRow(
          children: <Widget>[
            // Build #13 — hidden when the CP partner-directory switch is
            // off.
            if (partnerDirectoryEnabled)
              SimfNavTile(
                label: l10n.tileMeetPeople,
                iconAsset: HomeIcons.meetPeople,
                minHeight: SimfTokens.navTileHeight,
                onTap: () => context.pushNamed(RouteNames.meetPeople),
              ),
            SimfNavTile(
              label: l10n.chatbotTitle,
              iconAsset: HomeIcons.aiAssistant,
              minHeight: SimfTokens.navTileHeight,
              onTap: () => context.pushNamed(RouteNames.chatbot),
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space2),
        SimfTileRow(
          children: <Widget>[
            SimfNavTile(
              label: l10n.tileSessionSummary,
              iconAsset: HomeIcons.sessionSummary,
              minHeight: SimfTokens.navTileHeight,
              // Owner 2026-07-01: the smart-features "ملخص الجلسات" tile
              // opens the AI session-summaries list (Figma 1388:8392,
              // header "ملخص الجلسات") — matching its summary icon + label.
              // The session-downloads screen ("الجلسات", 1388:7621) is the
              // about tile above; My-Sessions (1388:9067) stays on My-Area.
              onTap: () => context.pushNamed(RouteNames.sessionSummaryList),
            ),
            SimfNavTile(
              label: l10n.tileEntryBadge,
              iconAsset: HomeIcons.badge,
              minHeight: SimfTokens.navTileHeight,
              onTap: () => context.pushNamed(RouteNames.badge),
            ),
          ],
        ),
        const SizedBox(height: SimfTokens.space6),
      ],
    );
  }
}
