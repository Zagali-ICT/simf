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
import '../../app/widgets/ksa_shell.dart';
import '../../app/widgets/simf_svg_icon.dart';
import '../../core/env/build_config.dart';
import '../auth/sign_out.dart';
import '../myarea/data/myarea_models.dart';
import '../myarea/data/myarea_repository.dart';

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
/// frame `1129:17224`: the navy [KsaPage] shell, a منطقتي profile header card
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
    final profile = signedIn
        ? ref.watch(_moreProfileProvider).asData?.value
        : null;

    return KsaPage(
      title: l10n.moreTitle,
      onBack: () => ksaBackOrHome(context),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space4,
          SimfTokens.space4,
          SimfTokens.space6,
        ),
        children: <Widget>[
          if (signedIn) ...<Widget>[
            _ProfileCard(
              name: profile?.identity.localizedName(l10n.isArabic) ??
                  auth.session.user.displayName,
              tier: profile?.identity.localizedTier(l10n.isArabic),
              onTap: () => context.pushNamed(RouteNames.myArea),
            ),
            const SizedBox(height: SimfTokens.space5),
          ],

          // معلومات الملتقى
          _MoreSection(
            title: l10n.moreSectionForumInfo,
            rows: <Widget>[
              _MoreRow(
                title: l10n.moreAbout,
                onTap: () => context.pushNamed(RouteNames.aboutForum),
              ),
              _MoreRow(
                title: l10n.moreForumGuide,
                onTap: () => context.pushNamed(RouteNames.forumGuide),
              ),
              _MoreRow(
                title: l10n.faqRowTitle,
                onTap: () => context.pushNamed(RouteNames.faq),
              ),
              if (routeAllowsRole(RouteNames.sessionPresentations, role))
                _MoreRow(
                  title: l10n.morePresentations,
                  onTap: () =>
                      context.pushNamed(RouteNames.sessionPresentations),
                ),
              _MoreRow(
                title: l10n.moreVisitSaudi,
                onTap: () => unawaited(
                  confirmThenLaunchExternal(context, BuildConfig.visitSaudiUrl),
                ),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space5),

          // الإعدادات
          _MoreSection(
            title: l10n.moreSectionSettings,
            rows: <Widget>[
              _MoreRow(
                title: l10n.moreLanguage,
                trailingValue: l10n.languageCurrentName,
                onTap: () => unawaited(
                  ref.read(localeControllerProvider.notifier).toggle(),
                ),
              ),
              _MoreRow(
                title: l10n.moreAccessibility,
                onTap: () => context.pushNamed(RouteNames.accessibility),
              ),
              _MoreRow(
                title: l10n.moreNotifications,
                onTap: () => context.pushNamed(RouteNames.notifications),
              ),
            ],
          ),
          const SizedBox(height: SimfTokens.space5),

          // قانوني
          _MoreSection(
            title: l10n.moreSectionLegal,
            rows: <Widget>[
              _MoreRow(
                title: l10n.moreTerms,
                onTap: () => context.pushNamed(RouteNames.terms),
              ),
              _MoreRow(
                title: l10n.contactUsTitle,
                onTap: () => context.pushNamed(RouteNames.contactUs),
              ),
              if (routeAllowsRole(RouteNames.rate, role))
                _MoreRow(
                  title: l10n.moreRateApp,
                  onTap: () => context.pushNamed(RouteNames.rate),
                ),
            ],
          ),

          if (signedIn) ...<Widget>[
            const SizedBox(height: SimfTokens.space6),
            Center(
              child: TextButton(
                onPressed: () =>
                    unawaited(confirmAndSignOut(context, ref, l10n)),
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

/// The منطقتي header card (frame 1129:16898): the user's avatar, the "منطقتي"
/// title over the "{name} · {tier}" sub-line, and the gold caret.
class _ProfileCard extends StatelessWidget {
  const _ProfileCard({required this.name, required this.tier, required this.onTap});

  final String name;
  final String? tier;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final subtitle = <String>[
      if (name.trim().isNotEmpty) name.trim(),
      if (tier != null && tier!.trim().isNotEmpty) tier!.trim(),
    ].join(' · ');
    return Material(
      color: SimfTokens.navyDeep,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space3),
          child: Row(
            children: <Widget>[
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      l10n.moreMyAreaCardTitle,
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: SimfTokens.textMd,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    if (subtitle.isNotEmpty) ...<Widget>[
                      const SizedBox(height: SimfTokens.space1),
                      Text(
                        subtitle,
                        maxLines: 1,
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
              const SizedBox(width: SimfTokens.space3),
              KsaAvatar(name: name, currentUser: true, size: 42),
              const SizedBox(width: SimfTokens.space2),
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

/// A titled group of nav rows (frames 1129:17… "معلومات الملتقى" etc.).
class _MoreSection extends StatelessWidget {
  const _MoreSection({required this.title, required this.rows});

  final String title;
  final List<Widget> rows;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        Padding(
          padding: const EdgeInsets.only(bottom: SimfTokens.space3),
          child: Text(
            title,
            textAlign: TextAlign.start,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textLg,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
        for (final (index, row) in rows.indexed) ...<Widget>[
          if (index > 0) const SizedBox(height: SimfTokens.space2),
          row,
        ],
      ],
    );
  }
}

/// One nav row (frame 1129:17225…): the navy-deep box with the title at the
/// inline start, an optional value, and the gold caret at the inline end.
class _MoreRow extends StatelessWidget {
  const _MoreRow({
    required this.title,
    required this.onTap,
    this.trailingValue,
  });

  final String title;
  final String? trailingValue;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.navyDeep,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        child: SizedBox(
          height: 48,
          child: Padding(
            padding:
                const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
            child: Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    title,
                    textAlign: TextAlign.start,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: SimfTokens.textMd,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ),
                if (trailingValue != null) ...<Widget>[
                  const SizedBox(width: SimfTokens.space2),
                  Text(
                    trailingValue!,
                    style: const TextStyle(
                      color: SimfTokens.beigeBorder,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                ],
                const SizedBox(width: SimfTokens.space2),
                const SimfSvgIcon(
                  'assets/icons/ic_caret_left.svg',
                  color: SimfTokens.accent,
                  size: 22,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
