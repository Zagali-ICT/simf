import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/localization/locale_controller.dart';
import '../../app/route_names.dart';
import '../../app/router.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/confirm_external_link.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../../core/env/build_config.dart';
import '../account/sign_out.dart';
import '../myarea/data/myarea_models.dart';
import '../myarea/data/myarea_repository.dart';
import 'widgets/more_list.dart';
import 'widgets/more_profile_card.dart';

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
/// us) route to the ComingSoon placeholder; استكشف الرياض opens VisitSaudi; the
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
    final role = signedIn ? auth.session.user.appRole : AppRole.guest;
    final profile =
        signedIn ? ref.watch(_moreProfileProvider).asData?.value : null;

    return SimfPageShell(
      title: l10n.moreTitle,
      onBack: () => backOrHome(context),
      body: ListView(
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
              onTap: () => context.pushNamed(RouteNames.myArea),
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
              // The "عروض الجلسات" my-sessions row (route myAreaSessions) was
              // removed 2026-07-04 (D-609, owner directive) — the My-sessions
              // screen was backed up as `.bk` and taken out of the app. The
              // downloadable-slides screen (1388:7621) stays reachable from the
              // Home "الجلسات" tile.
              MoreRow(
                title: l10n.moreVisitSaudi,
                onTap: () => unawaited(
                  confirmThenLaunchExternal(context, BuildConfig.visitSaudiUrl),
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
              MoreRow(
                title: l10n.moreNotifications,
                onTap: () => context.pushNamed(RouteNames.notifications),
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
                  style: const TextStyle(
                    color: SimfTokens.beigeBorder,
                    fontSize: SimfTokens.textMd,
                  ),
                ),
              ),
            ),
          ],
          const SizedBox(height: SimfTokens.space4),
          Center(
            child: Text(
              l10n.moreVersion,
              style: const TextStyle(
                color: SimfTokens.inkMuted,
                fontSize: SimfTokens.textSm,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
