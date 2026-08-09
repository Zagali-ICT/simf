import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/localization/locale_controller.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/router.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/confirm_external_link.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/env/build_config.dart';
import 'package:simf_app/core/startup/app_version_policy.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/account/sign_out.dart';
import 'package:simf_app/features/more/widgets/more_list.dart';
import 'package:simf_app/features/more/widgets/more_profile_card.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Best-effort profile for the منطقتي header card — the real name + tier live
/// App-side (not in the Identity-issued token). Resolves to null while loading /
/// on error (e.g. a not-yet-approved 403); the card then falls back to the
/// session display name and hides the tier.
final _moreProfileProvider =
    FutureProvider.autoDispose<MyAreaDashboard?>((ref) async {
  try {
    return await ref.watch(myAreaRepositoryProvider).getDashboard();
  } on ApiFailure {
    return null;
  }
});

/// Page 041 — المزيد · More (#41, `/more`, public). Pixel-parity to KSA Figma
/// frame `1129:17224`: the navy [SimfPageShell] shell, a منطقتي profile header card
/// (signed-in), three grouped sections (معلومات الملتقى / الإعدادات / قانوني)
/// of bordered nav rows, the تسجيل الخروج link (signed-in) and the static
/// version line. Unbuilt entries (Forum guide / FAQ / presentations / Contact
/// us) route to the ComingSoon placeholder; اكتشف السعودية opens VisitSaudi; the
/// اللغة row shows the current language and toggles it (D-464).
class MoreScreen extends ConsumerWidget {
  const MoreScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final auth = ref.watch(authControllerProvider);
    final signedIn = auth is AuthStateSignedIn;
    // D-519 — role-filter the attendee-only rows so a focused Staff/Moderator
    // never sees a dead link here (the slide-in MoreDrawer filters identically).
    // D-666 — a not-yet-approved account presents as guest, so the attendee-only
    // rows (rate) hide for it just like they do for a true guest.
    final role = signedIn ? auth.session.user.effectiveAppRole : AppRole.guest;
    final profile =
        signedIn ? ref.watch(_moreProfileProvider).asData?.value : null;

    return SimfPageShell(
      title: l10n.moreTitle,
      onBack: () => backOrHome(context),
      // The More menu (frame 1129:17224) has no header language pill; it
      // carries a language row instead (owner 2026-07-07).
      showLanguageToggle: false,
      body: SimfPullToRefresh(
        onRefresh: () => refreshAsync(ref, _moreProfileProvider.future),
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space4,
            SimfTokens.space4,
            SimfTokens.space4,
            SimfTokens.space6,
          ),
          children: <Widget>[
            if (signedIn) ...<Widget>[
              MoreProfileCard(
                name: profile?.identity.localizedName(l10n.isArabic) ??
                    auth.session.user.displayName,
                tier: profile?.identity.localizedTier(l10n.isArabic),
                // My Area IS the Profile tab (RouteNames.myArea). Use go, not push,
                // so this switches to the existing tab instead of stacking a second
                // My Area instance on top — that duplicate re-ran its own dashboard
                // load and hung blank forever (owner-reported, 2026-07-11).
                onTap: () => context.goNamed(RouteNames.myArea),
              ),
              const SizedBox(height: SimfTokens.space5),
            ],

            // معلومات الملتقى
            MoreSection(
              title: l10n.moreSectionForumInfo,
              rows: <Widget>[
                MoreRow(
                  title: l10n.moreAbout,
                  onTap: () => context.pushNamed(RouteNames.aboutForum),
                ),
                MoreRow(
                  title: l10n.moreForumGuide,
                  onTap: () => context.pushNamed(RouteNames.forumGuide),
                ),
                MoreRow(
                  title: l10n.faqRowTitle,
                  onTap: () => context.pushNamed(RouteNames.faq),
                ),
                // "عروض الجلسات" — My sessions (Figma 1388:9067). Restored
                // to the More menu 2026-07-09 (D-710, owner reversed the D-609
                // removal). The route is attendee-gated, so the row shows
                // only when a signed-in role may reach it.
                if (routeAllowsRole(RouteNames.myAreaSessions, role))
                  MoreRow(
                    title: l10n.mySessionsTitle,
                    onTap: () => context.pushNamed(RouteNames.myAreaSessions),
                  ),
                MoreRow(
                  title: l10n.moreVisitSaudi,
                  onTap: () => unawaited(
                    confirmThenLaunchExternal(
                      context,
                      BuildConfig.visitSaudiUrl,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: SimfTokens.space5),

            // الإعدادات
            MoreSection(
              title: l10n.moreSectionSettings,
              rows: <Widget>[
                MoreRow(
                  title: l10n.moreLanguage,
                  trailingValue: l10n.languageCurrentName,
                  onTap: () => unawaited(
                    ref.read(localeControllerProvider.notifier).toggle(),
                  ),
                ),
                MoreRow(
                  title: l10n.moreAccessibility,
                  onTap: () => context.pushNamed(RouteNames.accessibility),
                ),
                // Notifications are auth-only — hide from a not-logged-in guest so
                // the row doesn't dead-bounce to sign-in (D-669).
                if (signedIn)
                  MoreRow(
                    title: l10n.moreNotifications,
                    onTap: () => context.pushNamed(RouteNames.notifications),
                  ),
                // Reset password (signed-in only) — reuses the forgot→reset
                // flow: it emails a code, then reset sets the new password.
                // The known email is pre-filled so it isn't retyped (D-659).
                if (signedIn)
                  MoreRow(
                    title: l10n.moreResetPassword,
                    onTap: () => context.pushNamed(
                      RouteNames.forgotPassword,
                      queryParameters: <String, String>{
                        'email': auth.session.user.email,
                      },
                    ),
                  ),
              ],
            ),
            const SizedBox(height: SimfTokens.space5),

            // قانوني
            MoreSection(
              title: l10n.moreSectionLegal,
              rows: <Widget>[
                MoreRow(
                  title: l10n.moreTerms,
                  onTap: () => context.pushNamed(RouteNames.terms),
                ),
                MoreRow(
                  title: l10n.contactUsTitle,
                  onTap: () => context.pushNamed(RouteNames.contactUs),
                ),
                if (routeAllowsRole(RouteNames.rate, role))
                  MoreRow(
                    title: l10n.moreRateApp,
                    onTap: () => context.pushNamed(RouteNames.rate),
                  ),
              ],
            ),

            if (signedIn) ...<Widget>[
              const SizedBox(height: SimfTokens.space6),
              Center(
                child: TextButton(
                  onPressed: () => unawaited(confirmAndSignOut(context, ref, l10n)),
                  child: Text(
                    l10n.signOutLink,
                    style: SimfTokens.bodyBeigeMd,
                  ),
                ),
              ),
            ],
            const SizedBox(height: SimfTokens.space4),
            Center(
              child: Text(
                // D-736 — the real installed version (package_info_plus).
                l10n.moreVersionLine(ref.watch(installedAppVersionProvider)),
                style: SimfTokens.bodyInkMutedSm,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
