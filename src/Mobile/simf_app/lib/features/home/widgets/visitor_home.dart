import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/route_names.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_bottom_nav.dart';
import '../../../app/widgets/simf_page_shell.dart';
import '../../../core/organization_profile/organization_profile.dart';
import '../../banners/data/banner_models.dart';
import '../../live/data/current_live_session.dart';
import '../../news/data/news_models.dart';
import '../../news/news_article_screen.dart';
import 'discover_saudi_row.dart';
import 'follow_us_section.dart';
import 'greeting_header.dart';
import 'highlights_carousel.dart';
import 'home_banners.dart';
import 'home_hero_banner.dart';
import 'home_icons.dart';

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

  /// The active home banners (the rotating hero image source, #43). Empty → the
  /// hero falls back to the static discover photo.
  final List<PublicBannerItem> banners;

  /// The forum edition config for the hero overlay (name / theme / dates /
  /// location). Null while loading → the hero shows the discover copy.
  final OrgProfile? profile;

  /// Exhibitor (العارض) — the attendee home plus the lead-capture tools section
  /// (scan a visitor's QR + my visitors). D-519.
  final bool isExhibitor;

  /// Bi-Meeting rework — the "اللقاءات الثنائية" tile is shown to anyone entitled
  /// to request a meeting (speaker OR delegation flag); others don't see it (they
  /// can't reach the page).
  final bool canRequestMeetings;

  /// Build #13 — the "قابل أشخاص مثلك" tile is hidden when the CP switch for the
  /// partner directory is off (the feature is unavailable). Default true.
  final bool partnerDirectoryEnabled;

  @override
  Widget build(BuildContext context) {
    return SimfPageShell(
      tab: SimfTab.home,
      header: GreetingHeader(l10n: l10n, name: name),
      body: ListView(
        padding: const EdgeInsets.all(SimfTokens.space4),
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
          // The live banner (frame node 758:1150) — deep-links to the session
          // that is live right now (resolved on tap from the shared programme
          // cache) so it opens that session's feed, not the empty "no live
          // session" screen. With nothing live it opens id-less and falls back
          // to the live screen's event-wide stream.
          Consumer(
            builder: (context, ref, _) => LiveBanner(
              l10n: l10n,
              onTap: () async {
                final liveId =
                    await ref.read(currentLiveSessionIdProvider.future);
                if (!context.mounted) {
                  return;
                }
                unawaited(
                  context.pushNamed(
                    RouteNames.liveBroadcast,
                    queryParameters: liveId != null
                        ? <String, String>{RouteParams.sessionId: liveId}
                        : const <String, String>{},
                  ),
                );
              },
            ),
            onTap: () => context.pushNamed(
              RouteNames.liveBroadcast,
              queryParameters: <String, String>{
                RouteParams.liveUrl: 'asset:assets/videos/onboard_01.mp4',
              },
            ),
          ),
          const SizedBox(height: SimfTokens.space6),
          // Exhibitor (العارض) lead-capture tools — D-519. Shown only to the
          // Exhibitor role, above the shared attendee content.
          if (isExhibitor) ...<Widget>[
            SimfSectionHeader(title: l10n.exhibitorToolsSection),
            const SizedBox(height: SimfTokens.space4),
            SimfTileRow(
              children: <Widget>[
                SimfNavTile(
                  label: l10n.scanVisitorTitle,
                  icon: Icons.qr_code_scanner,
                  minHeight: SimfTokens.navTileHeight,
                  onTap: () => context.pushNamed(RouteNames.scanVisitor),
                ),
                SimfNavTile(
                  label: l10n.myVisitorsTitle,
                  icon: Icons.groups_outlined,
                  minHeight: SimfTokens.navTileHeight,
                  onTap: () => context.pushNamed(RouteNames.myVisitors),
                ),
              ],
            ),
            const SizedBox(height: SimfTokens.space6),
          ],
          // "عن الملتقى" section bar (758:1207) — opens About the forum.
          SimfLinkRow(
            title: l10n.homeAboutSection,
            onTap: () => context.pushNamed(RouteNames.aboutForum),
          ),
          const SizedBox(height: SimfTokens.space6),
          // About tiles (frame 758:1215, h72) — a 4-up grid of the shared tile,
          // the same SimfNavTile reused as grid columns. Right→left under RTL:
          // المتحدثون · الأجنحة · الوٝود · جلسات.
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
              // الوٝود — delegations sits in the about row (frame 758:1220) with
              // the design's exact formkit:people glyph (node 1408:10399).
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
                // ("ملخص الجلسات", 1388:8392) is the smart-features tile below;
                // the agenda lives on the bottom-nav sessions tab (labelled
                // "الجلسات" per nav component 206:1732 — the tab label only
                // shows when that tab is active, so Home renders it icon-only).
                onTap: () => context.pushNamed(RouteNames.sessionPresentations),
              ),
            ],
          ),
          // 16px gap inside the "عن الملتقى" group (frame 1054:12864 gap-16).
          const SizedBox(height: SimfTokens.space4),
          // The full-width "اسأل المحاور" tile (1052:12856) — send a question.
          SimfNavTile(
            label: l10n.tileAskModerator,
            iconAsset: HomeIcons.askModerator,
            onTap: () => context.pushNamed(RouteNames.sendQuestion),
          ),
          const SizedBox(height: SimfTokens.space6),
          // News tiles (758:1228, h80): right→left اللقاءات الثنائية · الأرشيٝ.
          // D-745 — "اللقاءات الثنائية" now opens the VIP-only bilateral-meetings
          // page ([RouteNames.meetings], Figma 1408:9726) and is hidden for
          // non-VIP; the requests history moved to My-Area. When hidden, الأرشيٝ
          // fills the row on its own (SimfTileRow expands each child).
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
          // "الميزات الذكية" (758:1158) — header + the المزيد link → More.
          SimfSectionHeader(
            title: l10n.homeSmartSection,
            moreLabel: l10n.moreTitle,
            onMore: () => context.pushNamed(RouteNames.more),
          ),
          const SizedBox(height: SimfTokens.space4),
          SimfTileRow(
            children: <Widget>[
              // Build #13 — hidden when the CP partner-directory switch is off.
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
                // Owner 2026-07-01: the smart-features "ملخص الجلسات" tile opens
                // the AI session-summaries list (Figma 1388:8392, header
                // "ملخص الجلسات") — matching its summary icon + label. The
                // session-downloads screen ("الجلسات", 1388:7621) is the about
                // tile above; My-Sessions (1388:9067) stays on My-Area.
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
          // ابرز الاحداث (frame node 758:1239) — the highlights carousel
          // (image + title slides, auto-advancing); hidden until a post exists.
          if (highlights.isNotEmpty) ...<Widget>[
            const SizedBox(height: SimfTokens.space6),
            SimfSectionHeader(title: l10n.featuredEventsSection),
            const SizedBox(height: SimfTokens.space4),
            HighlightsCarousel(
              l10n: l10n,
              items: highlights,
              baseUrl: baseUrl,
              onTap: (post) => Navigator.of(context).push(
                MaterialPageRoute<void>(
                  builder: (_) => NewsArticleScreen(newsId: post.id),
                ),
              ),
            ),
          ],
          const SizedBox(height: SimfTokens.space6),
          // "اكتشٝ" (758:1270) — header + the روح السعودية discover row.
          SimfSectionHeader(title: l10n.discoverSection),
          const SizedBox(height: SimfTokens.space4),
          DiscoverSaudiRow(l10n: l10n),
          // "تابعنا" (758:1183) — header + brand row + handle. Self-hiding when
          // no social link is set (owner 2026-06-27); owns its leading gap.
          FollowUsSection(l10n: l10n),
        ],
      ),
    );
  }
}
