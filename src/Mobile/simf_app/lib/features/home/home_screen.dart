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
import '../../app/widgets/ksa_shell.dart';
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
    // Best-effort latest post for the أحدث منشوراتنا teaser — null while loading
    // / on error / when there are no posts, in which case the section is hidden.
    final latestPost = ref.watch(newsListProvider).maybeWhen(
          data: (items) => items.isEmpty ? null : items.first,
          orElse: () => null,
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
      latestPost: latestPost,
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
    return KsaPage(
      title: l10n.homeGuestTitle,
      onBack: () => context.canPop()
          ? context.pop()
          : context.pushNamed(RouteNames.signIn),
      tab: SimfTab.home,
      showSweep: true,
      // The guest home carries no notifications bell (frame 758:2910) — a guest
      // has no personal notifications.
      showNotificationsBell: false,
      body: ListView(
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          _GuestBanner(l10n: l10n),
          const SizedBox(height: SimfTokens.space4),
          KsaTileRow(
            children: <Widget>[
              KsaNavTile(
                label: l10n.tileSessions,
                iconAsset: _HomeIcons.sessions,
                onTap: () => context.pushNamed(RouteNames.sessions),
              ),
              KsaNavTile(
                label: l10n.tileSpeakers,
                iconAsset: _HomeIcons.speakers,
                onTap: () => context.pushNamed(RouteNames.speakers),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space2),
          KsaTileRow(
            children: <Widget>[
              KsaNavTile(
                label: l10n.tileVenueMap,
                iconAsset: _HomeIcons.venueMap,
                onTap: () => context.pushNamed(RouteNames.venueMap),
              ),
              KsaNavTile(
                label: l10n.tileExhibition,
                iconAsset: _HomeIcons.exhibition,
                onTap: () => context.pushNamed(RouteNames.booths),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space4),
          // The locked smart-badge card — a visual cue that signing in
          // unlocks it; never tappable as a guest.
          KsaNavTile(
            label: l10n.tileMyBadgeShort,
            iconAsset: _HomeIcons.badge,
            enabled: false,
          ),
          const SizedBox(height: SimfTokens.space6),
          KsaSectionHeader(title: l10n.homeOpenInfoSection),
          const SizedBox(height: SimfTokens.space3),
          KsaListRow(
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
    return KsaPage(
      tab: SimfTab.home,
      title: l10n.homeTitle,
      body: ListView(
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          KsaListRow(
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
          KsaListRow(
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
    return KsaPage(
      tab: SimfTab.home,
      title: l10n.homeTitle,
      body: ListView(
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          KsaListRow(
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
    this.latestPost,
    this.isExhibitor = false,
  });

  final AppL10n l10n;
  final String name;
  final String baseUrl;
  final NewsListItem? latestPost;

  /// Exhibitor (العارض) — the attendee home plus the lead-capture tools section
  /// (scan a visitor's QR + my visitors). D-519.
  final bool isExhibitor;

  @override
  Widget build(BuildContext context) {
    return KsaPage(
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
            KsaSectionHeader(title: l10n.exhibitorToolsSection),
            const SizedBox(height: SimfTokens.space4),
            KsaTileRow(
              children: <Widget>[
                KsaNavTile(
                  label: l10n.scanVisitorTitle,
                  icon: Icons.qr_code_scanner,
                  minHeight: 80,
                  onTap: () => context.pushNamed(RouteNames.scanVisitor),
                ),
                KsaNavTile(
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
          KsaLinkRow(
            title: l10n.homeAboutSection,
            onTap: () => context.pushNamed(RouteNames.aboutForum),
          ),
          const SizedBox(height: SimfTokens.space6),
          // About tiles (frame 758:1215, h72) — a 4-up grid of the shared tile,
          // the same KsaNavTile reused as grid columns. Right→left under RTL:
          // المتحدثون · الأجنحة · الوفود · جلسات.
          KsaTileRow(
            children: <Widget>[
              KsaNavTile(
                label: l10n.tileSpeakers,
                iconAsset: _HomeIcons.people,
                onTap: () => context.pushNamed(RouteNames.speakers),
              ),
              KsaNavTile(
                // Home button title matches the screen header ("المعرض").
                label: l10n.tileExhibition,
                iconAsset: _HomeIcons.booths,
                onTap: () => context.pushNamed(RouteNames.booths),
              ),
              // الوفود — delegations sits in the about row (frame 758:1220) with
              // the design's exact formkit:people glyph (node 1408:10399).
              KsaNavTile(
                label: l10n.delegationsTitle,
                iconAsset: _HomeIcons.delegations,
                onTap: () => context.pushNamed(RouteNames.delegations),
              ),
              KsaNavTile(
                label: l10n.tileSessions,
                iconAsset: _HomeIcons.aboutSessions,
                onTap: () => context.pushNamed(RouteNames.sessions),
              ),
            ],
          ),
          // 16px gap inside the "عن الملتقى" group (frame 1054:12864 gap-16).
          const SizedBox(height: SimfTokens.space4),
          // The full-width "اسأل المحاور" tile (1052:12856) — send a question.
          KsaNavTile(
            label: l10n.tileAskModerator,
            iconAsset: _HomeIcons.askModerator,
            onTap: () => context.pushNamed(RouteNames.sendQuestion),
          ),
          const SizedBox(height: SimfTokens.space6),
          // News tiles (758:1228, h80): right→left اللقاءات الثنائية · الأرشيف.
          KsaTileRow(
            children: <Widget>[
              KsaNavTile(
                label: l10n.tileBilateralMeetings,
                iconAsset: _HomeIcons.bilateral,
                minHeight: 80,
                // اللقاءات الثنائية is not designed yet (owner 2026-06-21) — the
                // tile lands on the ComingSoon placeholder, not the gallery.
                onTap: () => context.pushNamed(RouteNames.bilateralMeetings),
              ),
              KsaNavTile(
                label: l10n.tileArchive,
                iconAsset: _HomeIcons.archive,
                minHeight: 80,
                onTap: () => context.pushNamed(RouteNames.archive),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space6),
          // "الميزات الذكية" (758:1158) — header + the المزيد link → More.
          KsaSectionHeader(
            title: l10n.homeSmartSection,
            moreLabel: l10n.moreTitle,
            onMore: () => context.pushNamed(RouteNames.more),
          ),
          const SizedBox(height: SimfTokens.space4),
          KsaTileRow(
            children: <Widget>[
              KsaNavTile(
                label: l10n.tileMeetPeople,
                iconAsset: _HomeIcons.meetPeople,
                minHeight: 80,
                onTap: () => context.pushNamed(RouteNames.meetPeople),
              ),
              KsaNavTile(
                label: l10n.chatbotTitle,
                iconAsset: _HomeIcons.aiAssistant,
                minHeight: 80,
                onTap: () => context.pushNamed(RouteNames.chatbot),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space2),
          KsaTileRow(
            children: <Widget>[
              KsaNavTile(
                label: l10n.tileSessionSummary,
                iconAsset: _HomeIcons.sessionSummary,
                minHeight: 80,
                // #1/#6 — the tile now opens the summaries LIST → tap a session →
                // its AI-summary details page (was: straight to the picker screen).
                onTap: () => context.pushNamed(RouteNames.sessionSummaryList),
              ),
              KsaNavTile(
                label: l10n.tileEntryBadge,
                iconAsset: _HomeIcons.badge,
                minHeight: 80,
                onTap: () => context.pushNamed(RouteNames.badge),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space6),
          // "الرعاة" section bar (1049:12844) — opens Sponsors.
          KsaLinkRow(
            title: l10n.tileSponsors,
            onTap: () => context.pushNamed(RouteNames.sponsors),
          ),
          const SizedBox(height: SimfTokens.space6),
          // "الأخبار والتغطية" section bar (758:1211) — opens News.
          KsaLinkRow(
            title: l10n.tileNews,
            onTap: () => context.pushNamed(RouteNames.news),
          ),
          // أحدث منشوراتنا (frame node 758:1238) — hidden until a post exists.
          if (latestPost != null) ...<Widget>[
            const SizedBox(height: SimfTokens.space6),
            // Frame node 758:1239 — the section heading is "ابرز الاحداث".
            KsaSectionHeader(title: l10n.featuredEventsSection),
            const SizedBox(height: SimfTokens.space4),
            _LatestPostCard(
              l10n: l10n,
              post: latestPost!,
              baseUrl: baseUrl,
              onTap: () => Navigator.of(context).push(
                MaterialPageRoute<void>(
                  builder: (_) => NewsArticleScreen(newsId: latestPost!.id),
                ),
              ),
            ),
          ],
          const SizedBox(height: SimfTokens.space6),
          // "اكتشف" (758:1270) — header + the روح السعودية discover row.
          KsaSectionHeader(title: l10n.discoverSection),
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
          // 2026-06-27). InkWell rides the KsaPage Scaffold's Material ancestor.
          Semantics(
            button: true,
            label: l10n.navProfile,
            child: InkWell(
              onTap: () => context.pushNamed(RouteNames.myArea),
              borderRadius:
                  const BorderRadius.all(Radius.circular(SimfTokens.radius)),
              child: KsaAvatar(name: name, currentUser: true),
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
          const KsaHeaderActions(showUnreadBadge: true),
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

/// The أحدث منشوراتنا teaser (frame node 758:1240): the source row (the gold
/// SIMF chip + the source name at the inline end, the @handle + relative time at
/// the inline start), the lead paragraph, and the post image. The image rides
/// the article's `NewsImage` asset via the D-357 anonymous route (navy fallback
/// when none). The frame's engagement counts (758:1252) are admin-entered data
/// deferred to Phase 2 — not faked here.
class _LatestPostCard extends StatelessWidget {
  const _LatestPostCard({
    required this.l10n,
    required this.post,
    required this.baseUrl,
    required this.onTap,
  });

  final AppL10n l10n;
  final NewsListItem post;
  final String baseUrl;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    // The frame shows one lead paragraph (the bold line is the source name, not
    // the article title); prefer the excerpt, fall back to the title.
    final body =
        post.localizedExcerpt(isArabic) ?? post.localizedTitle(isArabic);
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: Container(
        // Frame 758:1240 — px16 / py8, borderless navy card.
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space4,
          vertical: SimfTokens.space2,
        ),
        decoration: BoxDecoration(
          color: SimfTokens.navyDeep,
          borderRadius: BorderRadius.circular(SimfTokens.radius),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            const SizedBox(height: SimfTokens.space2),
            // Source row (758:1243): the gold chip + source name at the inline
            // end (right under RTL); the @handle + relative time at the start.
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: <Widget>[
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    // The round gold chip with white "SIMF" (758:1247).
                    Container(
                      width: 44,
                      height: 44,
                      alignment: Alignment.center,
                      decoration: const BoxDecoration(
                        color: SimfTokens.accent,
                        shape: BoxShape.circle,
                      ),
                      child: const Text(
                        'SIMF',
                        style: TextStyle(
                          color: Colors.white,
                          fontWeight: FontWeight.w600,
                          fontSize: SimfTokens.textMd,
                        ),
                      ),
                    ),
                    const SizedBox(width: SimfTokens.space2),
                    // Source name 14px Bold (758:1246).
                    Text(
                      l10n.postSourceName,
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.w700,
                        fontSize: SimfTokens.textMd,
                      ),
                    ),
                  ],
                ),
                // @handle · relative time (758:1244) — beige 12px.
                Flexible(
                  child: Text(
                    '${l10n.postSourceHandle} · '
                    '${homePostTime(l10n, post.publishedAt, DateTime.now().toUtc())}',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: SimfTokens.beigeBorder,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: SimfTokens.space4),
            // Lead paragraph (758:1249) — beige 14px, up to 3 lines.
            Text(
              body,
              maxLines: 3,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: SimfTokens.beigeBorder,
                fontSize: SimfTokens.textMd,
                height: 1.5,
              ),
            ),
            const SizedBox(height: SimfTokens.space4),
            // The post image (758:1250) — the NewsImage asset, navy fallback.
            _PostImage(
              imageUrl: '$baseUrl/app/assets/NewsImage/${post.id}/image',
            ),
            const SizedBox(height: SimfTokens.space2),
          ],
        ),
      ),
    );
  }
}

/// The latest-post image (frame node 758:1250): the article's `NewsImage` asset
/// (public anonymous D-357 route) in a 120-high navy box with a faint white
/// hairline and the frame's 4-radius corners. A spinner shows while it loads; a
/// navy image-glyph box is the no-image / fetch-failure fall-back (prod has no
/// uploaded news images yet, so the fall-back is the designed empty state).
class _PostImage extends StatelessWidget {
  const _PostImage({required this.imageUrl});

  final String imageUrl;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 120,
      clipBehavior: Clip.antiAlias,
      decoration: const BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radiusSmall)),
      ),
      foregroundDecoration: BoxDecoration(
        border: Border.all(color: SimfTokens.line2),
        borderRadius: const BorderRadius.all(
          Radius.circular(SimfTokens.radiusSmall),
        ),
      ),
      child: Image.network(
        imageUrl,
        fit: BoxFit.cover,
        width: double.infinity,
        gaplessPlayback: true,
        loadingBuilder: (context, child, progress) {
          if (progress == null) {
            return child;
          }
          return const Center(
            child: SizedBox(
              width: 18,
              height: 18,
              child: CircularProgressIndicator(strokeWidth: 2),
            ),
          );
        },
        errorBuilder: (context, error, stackTrace) => const Center(
          child: Icon(
            Icons.image_outlined,
            size: 28,
            color: SimfTokens.beigeBorder,
          ),
        ),
      ),
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
        KsaSectionHeader(title: l10n.followUsSection),
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
    return KsaListRow(
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
