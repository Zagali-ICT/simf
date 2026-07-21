import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../core/organization_profile/organization_profile.dart';
import '../../core/site_settings/site_settings.dart';
import '../account/data/profile_repository.dart';
import '../banners/data/banner_models.dart';
import '../banners/data/banners_repository.dart';
import '../myarea/data/myarea_models.dart';
import '../myarea/data/myarea_repository.dart';
import '../news/data/news_models.dart';
import '../news/news_screen.dart' show newsListProvider;
import 'widgets/guest_home.dart';
import 'widgets/operational_homes.dart';
import 'widgets/visitor_home.dart';

// Re-export the greeting helpers so `homeGreeting` / `homePostTime` stay
// importable from this file (the home widget tests reference them here).
export 'home_greeting.dart';

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
      return GuestHome(l10n: l10n, pendingApproval: pendingApproval);
    }
    // Focused operational roles (D-519): each lands on a home that surfaces only
    // its own pages, not the visitor experience.
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
    // D-745 — the "اللقاءات الثنائية" tile is VIP-only, so it is threaded down and
    // hidden for non-VIP (they can't reach the meetings page anyway).
    final isVip = ref.watch(currentUserIsVipProvider).value ?? false;
    // The rotating hero (#43): the active home banners + the edition config.
    // Both best-effort — the hero falls back to the static discover photo / copy
    // while loading or when nothing is configured.
    final banners = ref.watch(bannersProvider).maybeWhen(
          data: (items) => items,
          orElse: () => const <PublicBannerItem>[],
        );
    final orgProfile = ref.watch(orgProfileProvider);
    // Build #13 — the "Meet People Like You" tile is hidden when the CP switch is
    // off. Best-effort (default true / fail-open) while site-settings loads.
    final partnerDirectoryEnabled = ref.watch(siteSettingsProvider).maybeWhen(
          data: (s) => s.partnerDirectoryEnabled,
          orElse: () => true,
        );
    return VisitorHome(
      l10n: l10n,
      name: _greetingName(
        profile?.identity.localizedName(l10n.isArabic),
        user?.displayName,
      ),
      highlights: highlights,
      banners: banners,
      profile: orgProfile,
      baseUrl: baseUrl,
      isExhibitor: role == AppRole.exhibitor,
      isVip: isVip,
      partnerDirectoryEnabled: partnerDirectoryEnabled,
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
