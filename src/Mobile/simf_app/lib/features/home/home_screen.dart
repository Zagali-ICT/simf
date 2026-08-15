import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/core/site_settings/site_settings.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_app/features/banners/data/banner_models.dart';
import 'package:simf_app/features/banners/data/banners_repository.dart';
import 'package:simf_app/features/home/data/home_repository.dart';
import 'package:simf_app/features/home/home_greeting.dart';
import 'package:simf_app/features/home/widgets/guest_home.dart';
import 'package:simf_app/features/home/widgets/operational_homes.dart';
import 'package:simf_app/features/home/widgets/visitor_home.dart';
import 'package:simf_app/features/news/data/news_models.dart';
import 'package:simf_app/features/news/data/news_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

// Re-export the greeting helpers so `homeGreeting` / `homePostTime` stay
// importable from this file (the home widget tests reference them here).
export 'home_greeting.dart';

/// Page 013 — الرئيسية · Home (router / landing screen #13, `path=/`),
/// rebuilt to the KSA frames: guest = 758:2910 (owner-picked), signed-in =
/// **758:1134** (the live exact-parity frame).
///
/// One route, four states off the cached auth privilege: the **guest** layout
/// (also shown to a signed-in but unapproved account), the focused **staff**
/// and **moderator** operational homes (D-519), and the **visitor/exhibitor**
/// signed-in layout. Each layout lives in `widgets/`. Home carries no data of
/// its own beyond the best-effort unread-notification count (Page_013 L-5), the
/// best-effort greeting profile, and the best-effort highlights list (reusing
/// `GET /app/news`); the live banner stays static config (D10, L-6).
///
/// Route: `RouteNames.home`.
/// Data: [authControllerProvider], [bannersProvider],
///       [currentUserMeetingAccessProvider], [homeProfileProvider],
///       [newsListProvider], [orgProfileProvider], [simfDataConfigProvider],
///       [siteSettingsProvider].
/// Perf: no list — a single-screen layout.
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final auth = ref.watch(authControllerProvider);
    final user = auth is AuthStateSignedIn ? auth.session.user : null;
    // DEF-MOD-008 — read the SAME effective role the router's role-gate reads
    // (D-666: a not-yet-approved account presents as guest). Home already
    // short-circuited on [pendingApproval] below, so this is a no-op in
    // behaviour — but the raw `appRole` is the source of the mismatch that made
    // session detail offer a moderator affordance the router then bounced, and
    // one role source keeps the surfaces from drifting apart again.
    final role = user?.effectiveAppRole ?? AppRole.guest;
    // A signed-in but unapproved account (pending / rejected) has no
    // permissions, so it sees the same guest layout (owner 2026-06-27, frame
    // 758:2910) — with an "awaiting approval" note instead of the sign-in CTA.
    final pendingApproval =
        user != null && user.registrationStatus != RegistrationStatus.approved;

    if (role == AppRole.guest || pendingApproval) {
      return GuestHome(l10n: l10n, pendingApproval: pendingApproval);
    }
    // Focused operational roles (D-519): each lands on a home that surfaces
    // only its own pages, not the visitor experience.
    if (role == AppRole.staff) {
      return StaffHome(l10n: l10n);
    }
    if (role == AppRole.moderator) {
      return ModeratorHome(l10n: l10n);
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
    // Bi-Meeting rework — the "اللقاءات الثنائية" tile shows to anyone entitled
    // to request a meeting (speaker OR delegation flag); hidden otherwise (they
    // can't reach the meetings page anyway).
    final canRequestMeetings =
        ref.watch(currentUserMeetingAccessProvider).value?.any ?? false;
    // The rotating hero (#43): the active home banners + the edition config.
    // Both best-effort — the hero falls back to the static discover photo / copy
    // while loading or when nothing is configured.
    final banners = ref.watch(bannersProvider).maybeWhen(
          data: (items) => items,
          orElse: () => const <PublicBannerItem>[],
        );
    final orgProfile = ref.watch(orgProfileProvider);
    // Build #13 — the "Meet People Like You" tile is hidden when the CP switch
    // is off. Best-effort (default true / fail-open) while site-settings loads.
    final partnerDirectoryEnabled = ref.watch(siteSettingsProvider).maybeWhen(
          data: (s) => s.partnerDirectoryEnabled,
          orElse: () => true,
        );
    // Owner rule: every data page pulls to refresh. Home renders six
    // independent reads, so the gesture re-fetches all of them and holds the
    // spinner until the slowest settles.
    Future<void> onRefresh() async {
      ref
        ..invalidate(newsListProvider)
        ..invalidate(homeProfileProvider)
        ..invalidate(bannersProvider)
        // The meeting-access provider is a SELECTOR over the cached profile
        // read, so invalidating it alone would recompute off the same stale
        // row and the gesture would quietly stop re-fetching. Invalidate the
        // source instead.
        ..invalidate(myProfileProvider)
        ..invalidate(siteSettingsProvider);
      try {
        await Future.wait<void>(<Future<void>>[
          ref.read(orgProfileProvider.notifier).warm(),
          ref.read(newsListProvider.future),
          ref.read(homeProfileProvider.future),
          ref.read(bannersProvider.future),
          ref.read(currentUserMeetingAccessProvider.future),
          ref.read(siteSettingsProvider.future),
        ]);
      } on Object {
        // Every section above reads through `maybeWhen(orElse:)` and renders
        // its own fallback, so a failed section must not reject the refresh
        // future — that would surface as an unhandled error rather than an
        // empty section.
      }
    }

    return VisitorHome(
      l10n: l10n,
      onRefresh: onRefresh,
      name: _greetingName(
        profile?.identity.localizedName(isArabic: l10n.isArabic),
        user?.displayName,
        isVisitor: profile?.identity.isVisitor ?? true,
      ),
      highlights: highlights,
      banners: banners,
      profile: orgProfile,
      baseUrl: baseUrl,
      isExhibitor: role == AppRole.exhibitor,
      canRequestMeetings: canRequestMeetings,
      partnerDirectoryEnabled: partnerDirectoryEnabled,
    );
  }
}

/// The greeting name: the App profile name when known, otherwise a name-less
/// salute — never the email (the auth display name is the email for accounts
/// created without a separate display name).
///
/// Shortened for visitors only, by [greetingDisplayName].
String _greetingName(
  String? profileName,
  String? authName, {
  required bool isVisitor,
}) {
  final profile = profileName?.trim() ?? '';
  if (profile.isNotEmpty) {
    return greetingDisplayName(profile, isVisitor: isVisitor);
  }
  final auth = authName?.trim() ?? '';
  if (auth.contains('@')) {
    return '';
  }
  return greetingDisplayName(auth, isVisitor: isVisitor);
}
