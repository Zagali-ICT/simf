import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/more/widgets/more_footer.dart';
import 'package:simf_app/features/more/widgets/more_forum_info_section.dart';
import 'package:simf_app/features/more/widgets/more_legal_section.dart';
import 'package:simf_app/features/more/widgets/more_profile_card.dart';
import 'package:simf_app/features/more/widgets/more_settings_section.dart';
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

/// More — المزيد · route: `RouteNames.more` · Figma 1129:17224
/// D-464 — the اللغة row shows the current language and toggles it.
class MoreScreen extends ConsumerWidget {
  const MoreScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final auth = ref.watch(authControllerProvider);
    final signedIn = auth is AuthStateSignedIn;
    // D-519 — role-filter the attendee-only rows so a focused Staff/Moderator
    // never sees a dead link here (the slide-in MoreDrawer filters
    // identically). D-666 — a not-yet-approved account presents as guest, so
    // the attendee-only rows (rate) hide for it just like they do for a true
    // guest.
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
                name:
                    profile?.identity.localizedName(isArabic: l10n.isArabic) ??
                        auth.session.user.displayName,
                tier: profile?.identity.localizedTier(isArabic: l10n.isArabic),
                // My Area IS the Profile tab (RouteNames.myArea). Use go, not
                // push, so this switches to the existing tab instead of
                // stacking a second My Area instance on top — that duplicate
                // re-ran its own dashboard load and hung blank forever
                // (owner-reported, 2026-07-11).
                onTap: () => context.goNamed(RouteNames.myArea),
              ),
              const SizedBox(height: SimfTokens.space5),
            ],
            MoreForumInfoSection(role: role),
            const SizedBox(height: SimfTokens.space5),
            MoreSettingsSection(
              accountEmail: signedIn ? auth.session.user.email : null,
            ),
            const SizedBox(height: SimfTokens.space5),
            MoreLegalSection(role: role),
            MoreFooter(signedIn: signedIn),
          ],
        ),
      ),
    );
  }
}
