import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../app/widgets/simf_bottom_nav.dart';
import '../../core/env/build_config.dart';
import '../../core/external_link.dart';
import '../news/data/news_models.dart';
import '../news/news_article_screen.dart';
import '../news/news_screen.dart' show newsListProvider;
import '../notifications/data/notifications_repository.dart';

/// Page 013 — الرئيسية · Home (router / landing screen #13, `path=/`),
/// rebuilt to the KSA Wave-2 frames: guest = 512:1492 (2×2 option,
/// owner-picked), signed-in = 203:1236.
///
/// One route, two states off the cached auth privilege: the **guest** layout
/// (browse banner, 2×2 public tiles, the locked بطاقتي card, the open-info
/// rows, the gold sign-in button) and the **signed-in** layout (greeting
/// header with bell + menu, the live banner, the three tile sections, the
/// follow-us row, the **أحدث منشوراتنا** latest-post teaser, and the discover
/// card). Home carries no data of its own beyond the best-effort
/// unread-notification count (Page_013 L-5) and the best-effort latest-news
/// post (frame node 522:2345 — D-403, reusing `GET /app/news`); the live
/// banner stays static config (D10, L-6). The post card shows the real latest
/// news item (logo + source + relative time + headline + excerpt); the frame's
/// post image + engagement counts are omitted (no servable news-image route /
/// no engagement model — not faked). Social + Visit-Saudi links are
/// config-driven and inert while unset (the D-369 contract).
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final auth = ref.watch(authControllerProvider);
    final user = auth is AuthStateSignedIn ? auth.session.user : null;
    final isGuest = (user?.appRole ?? AppRole.guest) == AppRole.guest;

    if (isGuest) {
      return _GuestHome(l10n: l10n);
    }
    // Best-effort: any wire error resolves to 0 (Logic L-5).
    final unread = ref.watch(unreadNotificationCountProvider).maybeWhen(
          data: (count) => count,
          orElse: () => 0,
        );
    // Best-effort latest post for the أحدث منشوراتنا teaser — null while loading
    // / on error / when there are no posts, in which case the section is hidden.
    final latestPost = ref.watch(newsListProvider).maybeWhen(
          data: (items) => items.isEmpty ? null : items.first,
          orElse: () => null,
        );
    return _VisitorHome(
      l10n: l10n,
      name: user?.displayName ?? '',
      unread: unread,
      latestPost: latestPost,
    );
  }
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

/// Opens a configured link in the external browser (best-effort, D-369).
Future<void> _openLink(String url) =>
    launchExternalUri(Uri.parse(url), mode: LaunchMode.externalApplication);

// ---------------------------------------------------------------------------
// Guest layout (frame 512:1492 — "الرئيسية • ضيف", 2×2 option)
// ---------------------------------------------------------------------------

class _GuestHome extends StatelessWidget {
  const _GuestHome({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return KsaPage(
      title: l10n.homeGuestTitle,
      onBack: () => context.canPop()
          ? context.pop()
          : context.pushNamed(RouteNames.signIn),
      tab: SimfTab.home,
      showSweep: true,
      body: ListView(
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          _GuestBanner(l10n: l10n),
          const SizedBox(height: SimfTokens.space4),
          KsaTileRow(
            children: <Widget>[
              KsaNavTile(
                label: l10n.tileSessions,
                icon: Icons.calendar_today_outlined,
                onTap: () => context.pushNamed(RouteNames.sessions),
              ),
              KsaNavTile(
                label: l10n.tileSpeakers,
                icon: Icons.mic_none_outlined,
                onTap: () => context.pushNamed(RouteNames.speakers),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space2),
          KsaTileRow(
            children: <Widget>[
              KsaNavTile(
                label: l10n.tileVenueMap,
                icon: Icons.map_outlined,
                onTap: () => context.pushNamed(RouteNames.venueMap),
              ),
              KsaNavTile(
                label: l10n.tileExhibition,
                icon: Icons.grid_view_outlined,
                onTap: () => context.pushNamed(RouteNames.booths),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space4),
          // The locked smart-badge card — a visual cue that signing in
          // unlocks it; never tappable as a guest.
          KsaNavTile(
            label: l10n.tileMyBadgeShort,
            icon: Icons.badge_outlined,
            enabled: false,
          ),
          const SizedBox(height: SimfTokens.space6),
          KsaSectionHeader(title: l10n.homeOpenInfoSection),
          const SizedBox(height: SimfTokens.space3),
          KsaListRow(
            title: l10n.faqRowTitle,
            subtitle: l10n.faqRowSubtitle,
            badge: const Icon(
              Icons.help_outline,
              size: 32,
              color: Colors.white,
            ),
            // No app FAQ endpoint exists yet — the about page carries the
            // venue/event info this row promises (tracked follow-up).
            onTap: () => context.pushNamed(RouteNames.aboutForum),
          ),
          const SizedBox(height: SimfTokens.space4),
          _DiscoverSaudiRow(l10n: l10n),
          const SizedBox(height: SimfTokens.space6),
          FilledButton(
            onPressed: () => context.pushNamed(RouteNames.signIn),
            child: Text(l10n.guestSignInCta),
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
// Signed-in layout (frame 203:1236 — greeting home)
// ---------------------------------------------------------------------------

class _VisitorHome extends StatelessWidget {
  const _VisitorHome({
    required this.l10n,
    required this.name,
    required this.unread,
    this.latestPost,
  });

  final AppL10n l10n;
  final String name;
  final int unread;
  final NewsListItem? latestPost;

  @override
  Widget build(BuildContext context) {
    return KsaPage(
      tab: SimfTab.home,
      header: _GreetingHeader(l10n: l10n, name: name, unread: unread),
      body: ListView(
        padding: const EdgeInsets.all(SimfTokens.space4),
        children: <Widget>[
          _LiveBanner(
            l10n: l10n,
            onTap: () => context.pushNamed(RouteNames.liveBroadcast),
          ),
          const SizedBox(height: SimfTokens.space6),
          KsaSectionHeader(
            title: l10n.homeAboutSection,
            moreLabel: l10n.moreTitle,
            onMore: () => context.pushNamed(RouteNames.aboutForum),
          ),
          const SizedBox(height: SimfTokens.space3),
          KsaTileRow(
            children: <Widget>[
              KsaNavTile(
                label: l10n.tileSpeakers,
                icon: Icons.mic_none_outlined,
                onTap: () => context.pushNamed(RouteNames.speakers),
              ),
              KsaNavTile(
                label: l10n.tileBooths,
                icon: Icons.storefront_outlined,
                onTap: () => context.pushNamed(RouteNames.booths),
              ),
              KsaNavTile(
                label: l10n.tileSponsors,
                icon: Icons.workspace_premium_outlined,
                onTap: () => context.pushNamed(RouteNames.sponsors),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space6),
          KsaSectionHeader(
            title: l10n.tileNews,
            moreLabel: l10n.moreTitle,
            onMore: () => context.pushNamed(RouteNames.news),
          ),
          const SizedBox(height: SimfTokens.space3),
          KsaTileRow(
            children: <Widget>[
              KsaNavTile(
                label: l10n.tileBilateralMeetings,
                icon: Icons.videocam_outlined,
                onTap: () => context.pushNamed(RouteNames.gallery),
              ),
              KsaNavTile(
                label: l10n.tileArchive,
                icon: Icons.archive_outlined,
                onTap: () => context.pushNamed(RouteNames.archive),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space6),
          KsaSectionHeader(
            title: l10n.homeSmartSection,
            moreLabel: l10n.moreTitle,
            onMore: () => context.pushNamed(RouteNames.more),
          ),
          const SizedBox(height: SimfTokens.space3),
          KsaTileRow(
            children: <Widget>[
              KsaNavTile(
                label: l10n.tileMeetPeople,
                icon: Icons.people_outline,
                onTap: () => context.pushNamed(RouteNames.meetPeople),
              ),
              KsaNavTile(
                label: l10n.chatbotTitle,
                icon: Icons.chat_bubble_outline,
                onTap: () => context.pushNamed(RouteNames.chatbot),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space2),
          KsaTileRow(
            children: <Widget>[
              KsaNavTile(
                label: l10n.tileSessionSummary,
                icon: Icons.summarize_outlined,
                onTap: () => context.pushNamed(RouteNames.aiSummary),
              ),
              KsaNavTile(
                label: l10n.tileEntryBadge,
                icon: Icons.credit_card_outlined,
                onTap: () => context.pushNamed(RouteNames.badge),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space6),
          KsaSectionHeader(title: l10n.followUsSection),
          const SizedBox(height: SimfTokens.space3),
          const _SocialRow(),
          const SizedBox(height: SimfTokens.space3),
          Text(
            l10n.followUsHandle,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
            ),
          ),
          // أحدث منشوراتنا (frame node 522:2345) — hidden until a post exists.
          if (latestPost != null) ...<Widget>[
            const SizedBox(height: SimfTokens.space6),
            KsaSectionHeader(
              title: l10n.latestPostsSection,
              moreLabel: l10n.moreTitle,
              onMore: () => context.pushNamed(RouteNames.news),
            ),
            const SizedBox(height: SimfTokens.space3),
            _LatestPostCard(
              l10n: l10n,
              post: latestPost!,
              onTap: () => Navigator.of(context).push(
                MaterialPageRoute<void>(
                  builder: (_) => NewsArticleScreen(newsId: latestPost!.id),
                ),
              ),
            ),
          ],
          const SizedBox(height: SimfTokens.space6),
          KsaSectionHeader(title: l10n.discoverSection),
          const SizedBox(height: SimfTokens.space3),
          _DiscoverSaudiRow(l10n: l10n),
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
    required this.unread,
  });

  final AppL10n l10n;
  final String name;
  final int unread;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space2,
      ),
      child: Row(
        children: <Widget>[
          KsaAvatar(name: name),
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
                  '$name 👋',
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
          IconButton(
            tooltip: l10n.notificationsTooltip,
            onPressed: () => context.pushNamed(RouteNames.notifications),
            icon: Badge.count(
              count: unread,
              isLabelVisible: unread > 0,
              child: const Icon(
                Icons.notifications_none_outlined,
                color: Colors.white,
                size: 26,
              ),
            ),
          ),
          IconButton(
            tooltip: l10n.moreTitle,
            // Opens the shared side drawer (this header renders inside KsaPage's
            // Scaffold, so the nearest Scaffold is the shell's).
            onPressed: () => Scaffold.of(context).openDrawer(),
            icon: const Icon(Icons.menu, color: Colors.white, size: 26),
          ),
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
                    fontWeight: FontWeight.w500,
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
                    const SizedBox(height: SimfTokens.space1),
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
              const Icon(
                Icons.arrow_left,
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

/// The follow-us row (frame node 522:2215): five bordered buttons with the
/// design's brand glyphs. A button with no configured URL is inert (D-369).
/// The أحدث منشوراتنا teaser (frame node 522:2345): the SIMF mark, the source +
/// relative time, the headline and a short excerpt. The frame's post image and
/// engagement counts are intentionally omitted — there is no servable news-image
/// route and no like/comment/repost model, so neither is faked (D-403).
class _LatestPostCard extends StatelessWidget {
  const _LatestPostCard({
    required this.l10n,
    required this.post,
    required this.onTap,
  });

  final AppL10n l10n;
  final NewsListItem post;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    final excerpt = post.localizedExcerpt(isArabic);
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: Container(
        padding: const EdgeInsets.all(SimfTokens.space4),
        decoration: BoxDecoration(
          color: SimfTokens.navyDeep,
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          border: Border.all(color: SimfTokens.accent, width: 0.2),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                const KsaAvatar(name: 'SIMF', size: 40),
                const SizedBox(width: SimfTokens.space3),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        l10n.postSourceName,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          color: Colors.white,
                          fontWeight: FontWeight.w700,
                          fontSize: SimfTokens.textSm,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        homePostTime(
                          l10n,
                          post.publishedAt,
                          DateTime.now().toUtc(),
                        ),
                        style: const TextStyle(
                          color: SimfTokens.beigeBorder,
                          fontSize: SimfTokens.textXs,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: SimfTokens.space3),
            Text(
              post.localizedTitle(isArabic),
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w600,
                fontSize: SimfTokens.textMd,
              ),
            ),
            if (excerpt != null) ...<Widget>[
              const SizedBox(height: SimfTokens.space2),
              Text(
                excerpt,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: SimfTokens.beigeBorder,
                  fontSize: SimfTokens.textSm,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _SocialRow extends StatelessWidget {
  const _SocialRow();

  @override
  Widget build(BuildContext context) {
    const links = <(String, String)>[
      ('assets/images/social_x.png', BuildConfig.socialXUrl),
      ('assets/images/social_instagram.png', BuildConfig.socialInstagramUrl),
      ('assets/images/social_linkedin.png', BuildConfig.socialLinkedInUrl),
      ('assets/images/social_youtube.png', BuildConfig.socialYouTubeUrl),
      ('assets/images/social_tiktok.png', BuildConfig.socialTikTokUrl),
    ];
    return Row(
      children: <Widget>[
        for (final (index, (asset, url)) in links.indexed) ...<Widget>[
          if (index > 0) const SizedBox(width: SimfTokens.space4),
          Expanded(child: _SocialButton(asset: asset, url: url)),
        ],
      ],
    );
  }
}

class _SocialButton extends StatelessWidget {
  const _SocialButton({required this.asset, required this.url});

  final String asset;
  final String url;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(10),
        side: const BorderSide(color: SimfTokens.navyDeep, width: 0.8),
      ),
      child: InkWell(
        onTap: url.isEmpty ? null : () => unawaited(_openLink(url)),
        borderRadius: BorderRadius.circular(10),
        child: SizedBox(
          height: 48,
          child: Center(
            child: Image.asset(asset, width: 20, height: 20),
          ),
        ),
      ),
    );
  }
}

/// The روح السعودية discover row, shared by the guest + signed-in layouts —
/// opens the configured Visit-Saudi link externally.
class _DiscoverSaudiRow extends StatelessWidget {
  const _DiscoverSaudiRow({required this.l10n});

  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return KsaListRow(
      title: l10n.discoverSaudiTitle,
      subtitle: l10n.discoverSaudiSubtitle,
      badge: const Text(
        'KSA',
        style: TextStyle(
          color: Colors.white,
          fontSize: SimfTokens.textMd,
          fontWeight: FontWeight.w500,
        ),
      ),
      onTap: () => unawaited(_openLink(BuildConfig.visitSaudiUrl)),
    );
  }
}
