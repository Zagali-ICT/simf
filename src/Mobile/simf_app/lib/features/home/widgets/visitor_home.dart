import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_bottom_nav.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/banners/data/banner_models.dart';
import 'package:simf_app/features/home/widgets/discover_saudi_row.dart';
import 'package:simf_app/features/home/widgets/exhibitor_tools_section.dart';
import 'package:simf_app/features/home/widgets/follow_us_section.dart';
import 'package:simf_app/features/home/widgets/greeting_header.dart';
import 'package:simf_app/features/home/widgets/home_about_section.dart';
import 'package:simf_app/features/home/widgets/home_hero_banner.dart';
import 'package:simf_app/features/home/widgets/home_highlights_section.dart';
import 'package:simf_app/features/home/widgets/home_live_banner_link.dart';
import 'package:simf_app/features/home/widgets/home_smart_features_section.dart';
import 'package:simf_app/features/news/data/news_models.dart';

/// Signed-in layout (frame 758:1134 — greeting home, exact parity): the
/// greeting header, discover hero, live banner, the "عن الملتقى" section bar +
/// its tile group, the "الميزات الذكية" smart tiles, the "الرعاة" +
/// "الأخبار والتغطية" section bars, the highlights carousel, the discover row,
/// and the follow-us row.
class VisitorHome extends StatelessWidget {
  const VisitorHome({
    required this.l10n,
    required this.name,
    required this.baseUrl,
    required this.onRefresh,
    this.highlights = const <NewsListItem>[],
    this.banners = const <PublicBannerItem>[],
    this.profile,
    this.isExhibitor = false,
    this.canRequestMeetings = false,
    this.partnerDirectoryEnabled = true,
    super.key,
  });

  final AppL10n l10n;
  final String name;
  final String baseUrl;
  final List<NewsListItem> highlights;

  /// Re-fetches every provider the home body renders. Owner rule: every data
  /// page pulls to refresh.
  final Future<void> Function() onRefresh;

  /// The active home banners (the rotating hero image source, #43). Empty → the
  /// hero falls back to the static discover photo.
  final List<PublicBannerItem> banners;

  /// The forum edition config for the hero overlay (name / theme / dates /
  /// location). Null while loading → the hero shows the discover copy.
  final OrgProfile? profile;

  /// Exhibitor (العارض) — the attendee home plus the lead-capture tools section
  /// (scan a visitor's QR + my visitors). D-519.
  final bool isExhibitor;

  /// Bi-Meeting rework — the "اللقاءات الثنائية" tile is shown to anyone
  /// entitled to request a meeting (speaker OR delegation flag); others don't
  /// see it (they can't reach the page).
  final bool canRequestMeetings;

  /// Build #13 — the "قابل أشخاص مثلك" tile is hidden when the CP switch for
  /// the partner directory is off (the feature is unavailable). Default true.
  final bool partnerDirectoryEnabled;

  @override
  Widget build(BuildContext context) {
    return SimfPageShell(
      tab: SimfTab.home,
      header: GreetingHeader(l10n: l10n, name: name),
      body: SimfPullToRefresh(
        onRefresh: onRefresh,
        child: ListView(
          padding: const EdgeInsets.all(SimfTokens.space4),
          physics: const AlwaysScrollableScrollPhysics(),
          children: <Widget>[
            // The rotating edition hero (#43): forum name / theme / dates /
            // location overlaid on the CP-managed banner images; opens News.
            HomeHeroBanner(
              l10n: l10n,
              profile: profile,
              banners: banners,
              baseUrl: baseUrl,
              onTap: () => context.pushNamed(RouteNames.news),
            ),
            const SizedBox(height: SimfTokens.space6),
            HomeLiveBannerLink(l10n: l10n),
            const SizedBox(height: SimfTokens.space6),
            if (isExhibitor) ExhibitorToolsSection(l10n: l10n),
            HomeAboutSection(
              l10n: l10n,
              canRequestMeetings: canRequestMeetings,
            ),
            HomeSmartFeaturesSection(
              l10n: l10n,
              partnerDirectoryEnabled: partnerDirectoryEnabled,
            ),
            // "الرعاة" section bar (1049:12844) — opens Sponsors.
            SimfLinkRow(
              title: l10n.tileSponsors,
              onTap: () => context.pushNamed(RouteNames.sponsors),
            ),
            const SizedBox(height: SimfTokens.space6),
            // "الأخبار والتغطية" section bar (758:1211) — opens News.
            SimfLinkRow(
              title: l10n.tileNews,
              onTap: () => context.pushNamed(RouteNames.news),
            ),
            if (highlights.isNotEmpty)
              HomeHighlightsSection(
                l10n: l10n,
                items: highlights,
                baseUrl: baseUrl,
              ),
            const SizedBox(height: SimfTokens.space6),
            // "اكتشف" (758:1270) — header + the روح السعودية discover row.
            SimfSectionHeader(title: l10n.discoverSection),
            const SizedBox(height: SimfTokens.space4),
            DiscoverSaudiRow(l10n: l10n),
            // "تابعنا" (758:1183) — header + brand row + handle. Self-hiding
            // when no social link is set (owner 2026-06-27); owns its leading
            // gap.
            FollowUsSection(l10n: l10n),
          ],
        ),
      ),
    );
  }
}
