import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/confirm_external_link.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../app/widgets/simf_svg_icon.dart';
import '../../core/env/build_config.dart';
import '../../core/organization_profile/organization_profile.dart';
import '../myarea/data/myarea_models.dart';
import '../myarea/data/myarea_repository.dart';
import '../news/data/news_models.dart';
import '../news/news_article_screen.dart';
import '../news/news_screen.dart' show newsListProvider;

/// The signed-in home tile glyphs — the exact iconify SVGs from KSA frame
/// 758:1134 (no 1:1 Material equivalent), bundled and tinted to the tile colour.
class _HomeIcons {
  // "عن الملتقى" group (758:1216/1220/1224 + 1052:12856) — the exact Figma glyphs.
  static const String aboutSessions = 'assets/icons/home_about_sessions.svg';
  // ^ streamline-ultimate:team-meeting (node 1327:3446).
  static const String delegations =
      'assets/icons/home_delegations.svg'; // formkit:people (node 1408:10399)
  static const String booths = 'assets/icons/home_booths.svg'; // solar:chart
  static const String people = 'assets/icons/home_people.svg'; // bi:people
  static const String askModerator =
      'assets/icons/home_ask_moderator.svg'; // solar:user-outline
  // News + smart-feature tiles (758:1230/1234 + 758:1164/1170/1175/1179).
  static const String archive = 'assets/icons/home_archive.svg';
  static const String bilateral = 'assets/icons/home_bilateral.svg';
  static const String meetPeople = 'assets/icons/home_meet_people.svg';
  static const String aiAssistant = 'assets/icons/home_ai_assistant.svg';
  static const String sessionSummary = 'assets/icons/home_session_summary.svg';
  static const String badge = 'assets/icons/home_badge.svg';
  // Guest-home tile glyphs (frame 758:2910) — extracted line icons.
  static const String speakers = 'assets/icons/home_speakers.svg';
  static const String sessions = 'assets/icons/home_sessions.svg';
  static const String venueMap = 'assets/icons/home_map.svg';
  static const String exhibition = 'assets/icons/home_exhibition.svg';
}

/// Best-effort signed-in profile for the greeting (the real name + avatar live
/// App-side on the dashboard, not in the Identity-issued auth token). Resolves
/// to null while loading / on error (e.g. a not-yet-approved 403), in which
/// case the greeting falls back to a name-less salute (never the email).
final homeProfileProvider =
    FutureProvider.autoDispose<MyAreaDashboard?>((ref) async {
  try {
    return await ref.watch(myAreaRepositoryProvider).getDashboard();
  } on ApiFailure {
    return null;
  }
});

/// Page 013 — الرئيسية · Home (router / landing screen #13, `path=/`),
/// rebuilt to the KSA frames: guest = 512:1492 (2×2 option, owner-picked),
/// signed-in = **758:1134** (the live exact-parity frame).
///
/// One route, two states off the cached auth privilege: the **guest** layout
/// (browse banner, 2×2 public tiles, the locked بطاقتي card, the open-info
/// rows, the gold sign-in button) and the **signed-in** layout (greeting
/// header, discover hero, live banner, the "عن الملتقى" section bar + its tile
/// group, the "الميزات الذكية" smart tiles, the "الرعاة" + "الأخبار والتغطية"
/// section bars, the **أحدث منشوراتنا** latest-post teaser, the discover row,
/// and the follow-us row). Home carries no data of its own beyond the
/// best-effort unread-notification count (Page_013 L-5) and the best-effort
/// latest-news post (frame node 758:1240, reusing `GET /app/news`); the live
/// banner stays static config (D10, L-6). The post card shows the real latest
/// news item (source + relative time + body + the **NewsImage** asset via the
/// D-357 route); the frame's engagement counts are admin-entered data deferred
/// to Phase 2 (the row stays hidden until the wire carries them — not faked).
/// Social + Visit-Saudi links are config-driven and inert while unset (D-369).
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final auth = ref.watch(authControllerProvider);
    final user = auth is AuthStateSignedIn ? auth.session.user : null;
    final role = user?.appRole ?? AppRole.guest;
    // A signed-in but unapproved account (pending / rejected) has no
    // permissions, so it sees the same guest layout (owner 2026-06-27, frame
    // 758:2910) — with an "awaiting approval" note instead of the sign-in CTA.
    final pendingApproval =
        user != null && user.registrationStatus != RegistrationStatus.approved;

    if (role == AppRole.guest || pendingApproval) {
      return _GuestHome(l10n: l10n, pendingApproval: pendingApproval);
    }
    // Focused operational roles (D-519): each lands on a home that surfaces only
    // its own pages, not the visitor experience.
    if (role == AppRole.staff) {
      return _StaffHome(l10n: l10n);
    }
    if (role == AppRole.moderator) {
      return _ModeratorHome(l10n: l10n);
    }
    // ابرز الاحداث (758:1239) — the highlights carousel slides: the most recent
    // news items (image + title), shown as an animated carousel. Empty while
    // loading / on error / when there are no posts (the section then hides).
    // News is already CP-managed + persisted (/admin/news), so the carousel
    // needs no new table or API — it just renders the existing list.
    final highlights = ref.watch(newsListProvider).maybeWhen(
          data: (items) => items.take(6).toList(),
          orElse: () => const <NewsListItem>[],
        );
    // The greeting name + avatar come from the App profile (frame shows the
    // person's name, not the email). Best-effort: null until it loads.
    final profile = ref.watch(homeProfileProvider).maybeWhen(
          data: (dash) => dash,
          orElse: () => null,
        );
    // The post card builds `{base}/app/assets/NewsImage/{id}/image`; the base
    // already includes `/api/v1` (same anonymous D-357 route as the news list).
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    return _VisitorHome(
      l10n: l10n,
      name: _greetingName(
        profile?.identity.localizedName(l10n.isArabic),
        user?.displayName,
      ),
      highlights: highlights,
      baseUrl: baseUrl,
      isExhibitor: role == AppRole.exhibitor,
    );
  }
}

/// The greeting name: the App profile name when known, otherwise a name-less
/// salute — never the email (the auth display name is the email for accounts
/// created without a separate display name).
String _greetingName(String? profileName, String? authName) {
  final profile = profileName?.trim() ?? '';
  if (profile.isNotEmpty) {
    return profile;
  }
  final auth = authName?.trim() ?? '';
  return auth.contains('@') ? '' : auth;
}

/// The greeting word by local time of day (the frame's "صباح الخير" row).
String homeGreeting(AppL10n l10n, DateTime now) =>
    now.hour < 12 ? l10n.greetingMorning : l10n.greetingEvening;

/// The relative "time-ago" label for the latest-post card (the frame's
/// "قبل ساعة"). Buckets: just-now → minutes → hours → days.
String homePostTime(AppL10n l10n, DateTime publishedUtc, DateTime nowUtc) {
  final diff = nowUtc.difference(publishedUtc);
  if (diff.inMinutes < 1) {
    return l10n.postTimeJustNow;
  }
  if (diff.inHours < 1) {
    return l10n.postTimeMinutesAgo(diff.inMinutes);
  }
  if (diff.inHours < 24) {
    return l10n.postTimeHoursAgo(diff.inHours);
  }
  return l10n.postTimeDaysAgo(diff.inDays);
}

// ---------------------------------------------------------------------------
// Guest / unapproved layout (frame 758:2910 — "الرئيسية • ضيف", 2×2 tiles):
// shown to a not-signed-in guest AND a signed-in but unapproved account.
// ---------------------------------------------------------------------------

class _GuestHome extends StatelessWidget {
  const _GuestHome({required this.l10n, this.pendingApproval = false});

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
          _GuestBanner(l10n: l10n),
          const SizedBox(height: SimfTokens.space4),
          SimfTileRow(
            children: <Widget>[
              SimfNavTile(
                label: l10n.tileSessions,
                iconAsset: _HomeIcons.sessions,
                onTap: () => context.pushNamed(RouteNames.sessions),
              ),
              SimfNavTile(
                label: l10n.tileSpeakers,
                iconAsset: _HomeIcons.speakers,
                onTap: () => context.pushNamed(RouteNames.speakers),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space2),
          SimfTileRow(
            children: <Widget>[
              SimfNavTile(
                label: l10n.tileVenueMap,
                iconAsset: _HomeIcons.venueMap,
                onTap: () => context.pushNamed(RouteNames.venueMap),
              ),
              SimfNavTile(
                label: l10n.tileExhibition,
                iconAsset: _HomeIcons.exhibition,
                onTap: () => context.pushNamed(RouteNames.booths),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space4),
          // The locked smart-badge card — a visual cue that signing in
          // unlocks it; never tappable as a guest.
          SimfNavTile(
            label: l10n.tileMyBadgeShort,
            iconAsset: _HomeIcons.badge,
            enabled: false,
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
              size: 32,
              color: SimfTokens.accent,
            ),
            // Wave 1 added the public FAQ screen (GET /app/faq); the row opens
            // it directly now (was temporarily pointed at About before the
            // endpoint existed).
            onTap: () => context.pushNamed(RouteNames.faq),
          ),
          const SizedBox(height: SimfTokens.space4),
          _DiscoverSaudiRow(l10n: l10n, outlined: true),
          const SizedBox(height: SimfTokens.space6),
          // True guest → the sign-in CTA; signed-in-but-unapproved → the
          // "awaiting approval" note (a sign-in button would be wrong).
          if (pendingApproval)
            _PendingApprovalNote(l10n: l10n)
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

/// The "your account is awaiting approval" note shown in place of the sign-in
/// CTA when an unapproved (pending / rejected) account lands on the guest home
/// (owner 2026-06-27). The account is already signed in, so a sign-in button
/// would be wrong; full features unlock once the registration is approved.
class _PendingApprovalNote extends StatelessWidget {
  const _PendingApprovalNote({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space3),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(color: SimfTokens.accent, width: 0.5),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          const Icon(Icons.hourglass_top, color: SimfTokens.accent, size: 24),
          const SizedBox(width: SimfTokens.space2),
          Expanded(
            child: Text(
              l10n.homePendingApprovalNote,
              textAlign: TextAlign.start,
              style: const TextStyle(
                color: SimfTokens.beigeBorder,
                fontSize: SimfTokens.textMd,
                height: 1.5,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// The "you are browsing as a guest" banner: a navy card with the gold
/// highlighted phrase inside the beige copy (frame node 512:1499).
class _GuestBanner extends StatelessWidget {
  const _GuestBanner({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space3,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(color: SimfTokens.accent, width: 0.2),
      ),
      child: Text.rich(
        TextSpan(
          style: const TextStyle(
            color: SimfTokens.beigeBorder,
            fontSize: SimfTokens.textMd,
            height: 1.5,
          ),
          children: <InlineSpan>[
            TextSpan(text: l10n.guestBannerPrefix),
            TextSpan(
              text: l10n.guestBannerHighlight,
              style: const TextStyle(
                color: SimfTokens.accent,
                fontWeight: FontWeight.w600,
              ),
            ),
            TextSpan(text: l10n.guestBannerSuffix),
          ],
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Focused operational homes (D-519) — Staff + Moderator land here instead of the
// attendee home; each surfaces only its own pages.
// ---------------------------------------------------------------------------

/// Staff (gate) home — the two gate operations: scan a badge + register a
/// walk-in visitor. The attendee experience is intentionally absent.
class _StaffHome extends StatelessWidget {
  const _StaffHome({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return SimfPageShell(
      tab: SimfTab.home,
      title: l10n.homeTitle,
      body: ListView(
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          SimfListRow(
            title: l10n.gateScannerEntry,
            badgeOutlined: true,
            badge: const Icon(
              Icons.qr_code_scanner,
              size: 32,
              color: SimfTokens.accent,
            ),
            onTap: () => context.pushNamed(RouteNames.gateScanner),
          ),
          const SizedBox(height: SimfTokens.space4),
          SimfListRow(
            title: l10n.staffRegisterVisitorEntry,
            badgeOutlined: true,
            badge: const Icon(
              Icons.person_add_alt_1_outlined,
              size: 32,
              color: SimfTokens.accent,
            ),
            onTap: () => context.pushNamed(RouteNames.staffRegisterVisitor),
          ),
        ],
      ),
    );
  }
}

/// Moderator (محاور) home — a single entry into the sessions list, where the
/// moderator opens their session and runs its Q&A desk (reached from the session
/// detail; the server still enforces the per-session grant).
class _ModeratorHome extends StatelessWidget {
  const _ModeratorHome({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return SimfPageShell(
      tab: SimfTab.home,
      title: l10n.homeTitle,
      body: ListView(
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          SimfListRow(
            title: l10n.tileSessions,
            subtitle: l10n.moderatorManageQuestions,
            badgeOutlined: true,
            badge: const Icon(
              Icons.forum_outlined,
              size: 32,
              color: SimfTokens.accent,
            ),
            onTap: () => context.pushNamed(RouteNames.sessions),
          ),
        ],
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Signed-in layout (frame 758:1134 — greeting home, exact parity)
// ---------------------------------------------------------------------------

class _VisitorHome extends StatelessWidget {
  const _VisitorHome({
    required this.l10n,
    required this.name,
    required this.baseUrl,
    this.highlights = const <NewsListItem>[],
    this.isExhibitor = false,
  });

  final AppL10n l10n;
  final String name;
  final String baseUrl;
  final List<NewsListItem> highlights;

  /// Exhibitor (العارض) — the attendee home plus the lead-capture tools section
  /// (scan a visitor's QR + my visitors). D-519.
  final bool isExhibitor;

  @override
  Widget build(BuildContext context) {
    return SimfPageShell(
      tab: SimfTab.home,
      header: _GreetingHeader(
        l10n: l10n,
        name: name,
      ),
      body: ListView(
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          // The discovery hero banner (frame node 758:1203) — opens News.
          _DiscoverHeroBanner(
            l10n: l10n,
            onTap: () => context.pushNamed(RouteNames.news),
          ),
          const SizedBox(height: SimfTokens.space6),
          // The live banner (frame node 758:1150) — opens the live view.
          _LiveBanner(
            l10n: l10n,
            onTap: () => context.pushNamed(RouteNames.liveBroadcast),
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
                  minHeight: 80,
                  onTap: () => context.pushNamed(RouteNames.scanVisitor),
                ),
                SimfNavTile(
                  label: l10n.myVisitorsTitle,
                  icon: Icons.groups_outlined,
                  minHeight: 80,
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
          // المتحدثون · الأجنحة · الوفود · جلسات.
          SimfTileRow(
            children: <Widget>[
              SimfNavTile(
                label: l10n.tileSpeakers,
                iconAsset: _HomeIcons.people,
                onTap: () => context.pushNamed(RouteNames.speakers),
              ),
              SimfNavTile(
                // Home button title matches the screen header ("المعرض").
                label: l10n.tileExhibition,
                iconAsset: _HomeIcons.booths,
                onTap: () => context.pushNamed(RouteNames.booths),
              ),
              // الوفود — delegations sits in the about row (frame 758:1220) with
              // the design's exact formkit:people glyph (node 1408:10399).
              SimfNavTile(
                label: l10n.delegationsTitle,
                iconAsset: _HomeIcons.delegations,
                onTap: () => context.pushNamed(RouteNames.delegations),
              ),
              SimfNavTile(
                label: l10n.tileSessions,
                iconAsset: _HomeIcons.aboutSessions,
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
            iconAsset: _HomeIcons.askModerator,
            onTap: () => context.pushNamed(RouteNames.sendQuestion),
          ),
          const SizedBox(height: SimfTokens.space6),
          // News tiles (758:1228, h80): right→left اللقاءات الثنائية · الأرشيف.
          SimfTileRow(
            children: <Widget>[
              SimfNavTile(
                label: l10n.tileBilateralMeetings,
                iconAsset: _HomeIcons.bilateral,
                minHeight: 80,
                // اللقاءات الثنائية opens the الطلبات Requests feed (1408:9726) —
                // where bilateral / meeting requests live (owner 2026-06-29: keep
                // the tile label, link it to Requests instead of the ComingSoon).
                onTap: () => context.pushNamed(RouteNames.requests),
              ),
              SimfNavTile(
                label: l10n.tileArchive,
                iconAsset: _HomeIcons.archive,
                minHeight: 80,
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
              SimfNavTile(
                label: l10n.tileMeetPeople,
                iconAsset: _HomeIcons.meetPeople,
                minHeight: 80,
                onTap: () => context.pushNamed(RouteNames.meetPeople),
              ),
              SimfNavTile(
                label: l10n.chatbotTitle,
                iconAsset: _HomeIcons.aiAssistant,
                minHeight: 80,
                onTap: () => context.pushNamed(RouteNames.chatbot),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space2),
          SimfTileRow(
            children: <Widget>[
              SimfNavTile(
                label: l10n.tileSessionSummary,
                iconAsset: _HomeIcons.sessionSummary,
                minHeight: 80,
                // Owner 2026-07-01: the smart-features "ملخص الجلسات" tile opens
                // the AI session-summaries list (Figma 1388:8392, header
                // "ملخص الجلسات") — matching its summary icon + label. The
                // session-downloads screen ("الجلسات", 1388:7621) is the about
                // tile above; My-Sessions (1388:9067) stays on My-Area.
                onTap: () => context.pushNamed(RouteNames.sessionSummaryList),
              ),
              SimfNavTile(
                label: l10n.tileEntryBadge,
                iconAsset: _HomeIcons.badge,
                minHeight: 80,
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
            _HighlightsCarousel(
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
          // "اكتشف" (758:1270) — header + the روح السعودية discover row.
          SimfSectionHeader(title: l10n.discoverSection),
          const SizedBox(height: SimfTokens.space4),
          _DiscoverSaudiRow(l10n: l10n),
          // "تابعنا" (758:1183) — header + brand row + handle. Self-hiding when
          // no social link is set (owner 2026-06-27); owns its leading gap.
          _FollowUsSection(l10n: l10n),
        ],
      ),
    );
  }
}

/// The greeting header (frame node 203:1238): avatar + greeting + name at the
/// inline start; the bell (with the unread badge) and the menu at the end.
class _GreetingHeader extends StatelessWidget {
  const _GreetingHeader({
    required this.l10n,
    required this.name,
  });

  final AppL10n l10n;
  final String name;

  @override
  Widget build(BuildContext context) {
    // Name-less while the profile loads (or for a name-less account) → just the
    // wave, never a stray leading space.
    final nameLine = name.isEmpty ? '👋' : '$name 👋';
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space2,
      ),
      child: Row(
        children: <Widget>[
          // Tapping the avatar opens the user's profile / My Area (owner
          // 2026-06-27). InkWell rides the SimfPageShell Scaffold's Material ancestor.
          Semantics(
            button: true,
            label: l10n.navProfile,
            child: InkWell(
              onTap: () => context.pushNamed(RouteNames.myArea),
              borderRadius:
                  const BorderRadius.all(Radius.circular(SimfTokens.radius)),
              child: SimfAvatar(name: name, currentUser: true),
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  homeGreeting(l10n, DateTime.now()),
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: SimfTokens.textMd,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  nameLine,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: SimfTokens.accent,
                    fontSize: SimfTokens.textLg,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
          // The shared top-nav action cluster — identical to every sub-page:
          // the bell, the language globe, the dark-mode crescent, and the menu
          // ☰, each a gold glyph in a navy box. Home carries the unread badge.
          const SimfHeaderActions(showUnreadBadge: true),
        ],
      ),
    );
  }
}

/// The red LIVE banner (frame node 210:736) — static config for now (no API,
/// D10, Page_013 L-6); tapping it opens the live view.
class _LiveBanner extends StatelessWidget {
  const _LiveBanner({required this.l10n, required this.onTap});

  final AppL10n l10n;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: const BorderSide(color: SimfTokens.danger, width: 0.5),
      ),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2),
          child: Row(
            children: <Widget>[
              Container(
                width: 60,
                height: 60,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: SimfTokens.danger,
                  borderRadius: BorderRadius.circular(SimfTokens.radius),
                ),
                child: Text(
                  l10n.liveNowLabel,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: SimfTokens.textMd,
                    // Frame 758:1157 — the "مباشر" badge is SemiBold.
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
              const SizedBox(width: SimfTokens.space3),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      l10n.homeLiveTitle,
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.w600,
                        fontSize: SimfTokens.textMd,
                      ),
                    ),
                    const SizedBox(height: SimfTokens.space2),
                    Text(
                      l10n.homeLiveSubtitle,
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: SimfTokens.textSm,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              // Owner 2026-06-27 — the LIVE (YouTube/broadcast) banner's caret
              // must match the "عن الملتقى" / section rows: the same gold
              // ic_caret_left.svg (not a white Material arrow). The bundled SVG
              // points left and does not mirror under RTL.
              const SimfSvgIcon(
                'assets/icons/ic_caret_left.svg',
                color: SimfTokens.accent,
                size: 24,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The top discovery hero banner (frame node 758:1203): the bundled event
/// photo under a 70% black scrim, with the gold "اكتشف" title and the white
/// sub-line right-aligned. Tapping it opens News.
class _DiscoverHeroBanner extends StatelessWidget {
  const _DiscoverHeroBanner({required this.l10n, required this.onTap});

  final AppL10n l10n;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // اكتشف hero (frame 758:1203). Taller (160) so on a wide tablet the full
    // photo is visible (96 made it an ultra-thin strip), and BoxFit.fill so the
    // whole image stretches into the banner — "view the full image" (owner
    // 2026-06-27). The outer SizedBox + StackFit.expand give the Stack a definite
    // size so every layer fills edge-to-edge. Scrim lightened to ~50% so the
    // photo reads clearly (the 70% black hid it).
    return SizedBox(
      height: 160,
      width: double.infinity,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Stack(
          fit: StackFit.expand,
          children: <Widget>[
            Image.asset(
              'assets/images/discover_hero.jpg',
              fit: BoxFit.fill,
            ),
            const ColoredBox(color: Color(0x80000000)),
            Material(
              color: Colors.transparent,
              child: InkWell(
                onTap: onTap,
                child: Padding(
                  padding: const EdgeInsets.all(SimfTokens.space2),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        l10n.discoverSection,
                        style: const TextStyle(
                          color: SimfTokens.accent,
                          fontWeight: FontWeight.w700,
                          fontSize: SimfTokens.textLg,
                        ),
                      ),
                      const SizedBox(height: SimfTokens.space2),
                      Text(
                        l10n.discoverBannerSubtitle,
                        style: const TextStyle(
                          color: Colors.white,
                          fontWeight: FontWeight.w500,
                          fontSize: SimfTokens.textSm,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// ابرز الاحداث — the highlights carousel (frame node 758:1239): an
/// auto-advancing, swipeable PageView of image+title slides drawn from the most
/// recent news items (CP-managed via /admin/news). A row of dots tracks the
/// position. Owner spec (2026-06-28): "multiple slides, image and text only,
/// animated, entered via the Control Panel" — the old single image becomes a
/// gallery; news already backs it, so no new table or API is needed.
class _HighlightsCarousel extends StatefulWidget {
  const _HighlightsCarousel({
    required this.l10n,
    required this.items,
    required this.baseUrl,
    required this.onTap,
  });

  final AppL10n l10n;
  final List<NewsListItem> items;
  final String baseUrl;
  final void Function(NewsListItem) onTap;

  @override
  State<_HighlightsCarousel> createState() => _HighlightsCarouselState();
}

class _HighlightsCarouselState extends State<_HighlightsCarousel> {
  static const double _slideHeight = 170;
  static const Duration _interval = Duration(seconds: 4);

  late final PageController _controller;
  Timer? _timer;
  int _index = 0;

  @override
  void initState() {
    super.initState();
    _controller = PageController();
    _startAutoAdvance();
  }

  // Auto-advance to the next slide every [_interval], wrapping at the end. Only
  // runs when there is more than one slide.
  void _startAutoAdvance() {
    if (widget.items.length <= 1) {
      return;
    }
    _timer = Timer.periodic(_interval, (_) {
      if (!mounted || !_controller.hasClients) {
        return;
      }
      final next = (_index + 1) % widget.items.length;
      _controller.animateToPage(
        next,
        duration: const Duration(milliseconds: 450),
        curve: Curves.easeInOut,
      );
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        SizedBox(
          height: _slideHeight,
          child: PageView.builder(
            controller: _controller,
            onPageChanged: (i) => setState(() => _index = i),
            itemCount: widget.items.length,
            itemBuilder: (context, i) {
              final post = widget.items[i];
              return _HighlightSlide(
                title: post.localizedTitle(widget.l10n.isArabic),
                imageUrl:
                    '${widget.baseUrl}/app/assets/NewsImage/${post.id}/image',
                onTap: () => widget.onTap(post),
              );
            },
          ),
        ),
        if (widget.items.length > 1) ...<Widget>[
          const SizedBox(height: SimfTokens.space3),
          _CarouselDots(count: widget.items.length, index: _index),
        ],
      ],
    );
  }
}

/// One carousel slide (758:1239): the news image filling a rounded card with a
/// bottom scrim and the title overlaid — image + text only. Tapping opens the
/// article. The image rides the D-357 anonymous `NewsImage` route; a spinner
/// shows while it loads and a navy image-glyph box is the no-image fall-back.
class _HighlightSlide extends StatelessWidget {
  const _HighlightSlide({
    required this.title,
    required this.imageUrl,
    required this.onTap,
  });

  final String title;
  final String imageUrl;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Padding(
      // A small inset so the neighbouring slides peek at the edges.
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space1),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          child: Stack(
            fit: StackFit.expand,
            children: <Widget>[
              ColoredBox(
                color: SimfTokens.navy,
                child: Image.network(
                  imageUrl,
                  fit: BoxFit.cover,
                  gaplessPlayback: true,
                  loadingBuilder: (context, child, progress) =>
                      progress == null
                          ? child
                          : const Center(
                              child: SizedBox(
                                width: 18,
                                height: 18,
                                child:
                                    CircularProgressIndicator(strokeWidth: 2),
                              ),
                            ),
                  errorBuilder: (context, error, stackTrace) => const Center(
                    child: Icon(
                      Icons.image_outlined,
                      size: 28,
                      color: SimfTokens.beigeBorder,
                    ),
                  ),
                ),
              ),
              // Bottom scrim so the white title reads over any image.
              const DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.center,
                    end: Alignment.bottomCenter,
                    colors: <Color>[Colors.transparent, Color(0xCC01132D)],
                  ),
                ),
              ),
              Positioned(
                left: SimfTokens.space4,
                right: SimfTokens.space4,
                bottom: SimfTokens.space3,
                child: Text(
                  title,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w700,
                    fontSize: SimfTokens.textLg,
                    height: 1.3,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The carousel position dots — the active one is a wider gold pill, the rest
/// are faint beige.
class _CarouselDots extends StatelessWidget {
  const _CarouselDots({required this.count, required this.index});

  final int count;
  final int index;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: List<Widget>.generate(count, (i) {
        final active = i == index;
        return AnimatedContainer(
          duration: const Duration(milliseconds: 250),
          margin: const EdgeInsets.symmetric(horizontal: 3),
          width: active ? 16 : 6,
          height: 6,
          decoration: BoxDecoration(
            color: active
                ? SimfTokens.accent
                : SimfTokens.beigeBorder.withValues(alpha: 0.4),
            borderRadius: BorderRadius.circular(3),
          ),
        );
      }),
    );
  }
}

/// The "تابعنا" follow-us section (frame node 758:1183): the header, the brand
/// row and the @handle line. The links come from the CP-editable Organization
/// profile (downloaded at app start, cached, shared with About / Contact).
///
/// Owner 2026-06-27: a platform with **no URL is hidden** (not a dead/inert
/// button), and when **no** social link is set the whole section disappears
/// (header + row + handle). A set link, when tapped, asks to confirm leaving the
/// app, then opens it externally. Owns its leading gap so the layout stays tidy
/// whether it shows or hides.
class _FollowUsSection extends ConsumerWidget {
  const _FollowUsSection({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final social = ref.watch(orgProfileProvider)?.social;
    // (asset, url, label) — the exact Figma beige glyphs (node 758:1186); kept
    // only when the URL is set so an unconfigured platform is hidden, not inert.
    final links = <(String, String, String)>[
      ('assets/icons/social_x.svg', social?.x ?? BuildConfig.socialXUrl, 'X'),
      (
        'assets/icons/social_instagram.svg',
        social?.instagram ?? BuildConfig.socialInstagramUrl,
        'Instagram',
      ),
      (
        'assets/icons/social_linkedin.svg',
        social?.linkedin ?? BuildConfig.socialLinkedInUrl,
        'LinkedIn',
      ),
      (
        'assets/icons/social_youtube.svg',
        social?.youtube ?? BuildConfig.socialYouTubeUrl,
        'YouTube',
      ),
      (
        'assets/icons/social_tiktok.svg',
        social?.tiktok ?? BuildConfig.socialTikTokUrl,
        'TikTok',
      ),
    ].where((l) => l.$2.trim().isNotEmpty).toList();

    // No social link set → hide the entire section.
    if (links.isEmpty) {
      return const SizedBox.shrink();
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        const SizedBox(height: SimfTokens.space6),
        SimfSectionHeader(title: l10n.followUsSection),
        const SizedBox(height: SimfTokens.space4),
        // The brand row stays LTR (X · Instagram · … · TikTok) in any locale.
        Directionality(
          textDirection: TextDirection.ltr,
          child: Row(
            children: <Widget>[
              for (final (index, (asset, url, label)) in links.indexed)
                ...<Widget>[
                if (index > 0) const SizedBox(width: SimfTokens.space4),
                Expanded(
                  child: _SocialButton(asset: asset, url: url, label: label),
                ),
              ],
            ],
          ),
        ),
        const SizedBox(height: SimfTokens.space2),
        Text(
          l10n.followUsHandle,
          textAlign: TextAlign.center,
          // Frame 758:1202 — handle line is Medium, beige.
          style: const TextStyle(
            color: SimfTokens.beigeBorder,
            fontSize: SimfTokens.textSm,
            fontWeight: FontWeight.w500,
          ),
        ),
      ],
    );
  }
}

class _SocialButton extends StatelessWidget {
  const _SocialButton({
    required this.asset,
    required this.url,
    required this.label,
  });

  final String asset;
  final String url;

  /// The platform name, exposed to screen readers via the image's semantic
  /// label (the button is otherwise icon-only).
  final String label;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(10),
        side: const BorderSide(color: SimfTokens.navyDeep, width: 0.8),
      ),
      child: InkWell(
        onTap: url.isEmpty
            ? null
            : () => unawaited(confirmThenLaunchExternal(context, url)),
        borderRadius: BorderRadius.circular(10),
        child: SizedBox(
          height: 48,
          child: Semantics(
            button: true,
            label: label,
            child: Center(
              child: SimfSvgIcon(
                asset,
                size: 20,
                color: SimfTokens.beigeBorder,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// The روح السعودية discover row, shared by the guest + signed-in layouts —
/// opens the configured Visit-Saudi link externally.
class _DiscoverSaudiRow extends StatelessWidget {
  const _DiscoverSaudiRow({required this.l10n, this.outlined = false});

  final AppL10n l10n;

  /// The guest home uses the **outlined** badge (gold hairline + gold "KSA"),
  /// matching frame 758:2910; the signed-in home keeps the filled gold badge.
  final bool outlined;

  @override
  Widget build(BuildContext context) {
    return SimfListRow(
      title: l10n.discoverSaudiTitle,
      subtitle: l10n.discoverSaudiSubtitle,
      badgeOutlined: outlined,
      // Guest = outlined gold "KSA" (758:2910); signed-in = filled gold
      // "السعودية" SemiBold (758:1280/1281).
      badge: Text(
        outlined ? 'KSA' : l10n.discoverSaudiBadge,
        textAlign: TextAlign.center,
        style: TextStyle(
          color: outlined ? SimfTokens.accent : Colors.white,
          fontSize: SimfTokens.textMd,
          fontWeight: outlined ? FontWeight.w800 : FontWeight.w600,
        ),
      ),
      onTap: () => unawaited(
        confirmThenLaunchExternal(context, BuildConfig.visitSaudiUrl),
      ),
    );
  }
}
