import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/sponsors/data/sponsor_models.dart';
import 'package:simf_app/features/sponsors/data/sponsors_repository.dart';
import 'package:simf_app/features/sponsors/widgets/sponsor_card.dart';
import 'package:simf_app/features/sponsors/widgets/sponsor_grid.dart';
import 'package:simf_app/features/sponsors/widgets/sponsor_logo.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Page 023 — الرعاة · Sponsors (#23, `/sponsors`, Guest+), rebuilt to the
/// KSA-Project Figma frame **922:2824 "Shepherds"** on the shared navy shell.
///
/// **Public.** Behaviour/data contract unchanged: one read returns the sponsors
/// grouped by tier (`SponsorTierGroup`), and the screen renders one section per
/// non-empty tier in the order the API returns them. Frame mapping (922:2824 —
/// three bands, position-based so it is faithful for any tier naming): the
/// **first** tier renders the gold hero card; the **lowest** tier (the last
/// group, when more than one) renders the compact 3-column logo grid; any tier
/// **in between** renders the navy premium card. P6 — D-440: each sponsor logo is
/// the real `SponsorLogo` asset (D-357) served anonymously at
/// `{base}/app/assets/SponsorLogo/{id}/image`, with the acronym initials as the
/// fallback. The loading / error / empty / RTL states are preserved.
class SponsorsScreen extends ConsumerWidget {
  const SponsorsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final groups = ref.watch(sponsorGroupsProvider);
    // Pull-to-refresh — re-fetch the tier-grouped sponsors, holding the spinner
    // until the new read completes.
    Future<void> onRefresh() => refreshAsync(ref, sponsorGroupsProvider.future);

    return SimfPageShell(
      title: l10n.sponsorsTitle,
      onBack: () => backOrHome(context),
      showSweep: true,
      body: groups.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (_, __) => SimfRefreshableMessage(
          onRefresh: onRefresh,
          child: SimfErrorState(
            message: l10n.sponsorsError,
            retryLabel: l10n.retryLabel,
            onRetry: () => ref.invalidate(sponsorGroupsProvider),
          ),
        ),
        data: (data) {
          final visibleGroups = visibleTiers(data);
          if (visibleGroups.isEmpty) {
            return SimfRefreshableMessage(
              onRefresh: onRefresh,
              child: SimfEmptyState(
                icon: Icons.workspace_premium_outlined,
                message: l10n.sponsorsEmpty,
              ),
            );
          }
          final isArabic = l10n.isArabic;
          // The logo image lives at {base}/app/assets/SponsorLogo/{id}/image (D-357).
          final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
          final lastIndex = visibleGroups.length - 1;
          return SimfPullToRefresh(
            onRefresh: onRefresh,
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.all(SimfTokens.space4),
              children: <Widget>[
                for (var i = 0; i < visibleGroups.length; i++) ...<Widget>[
                  if (i > 0) const SizedBox(height: SimfTokens.space6),
                  SimfSectionHeader(
                    title: l10n.sponsorTierLabel(
                      visibleGroups[i].tier,
                      visibleGroups[i].tierName,
                    ),
                  ),
                  const SizedBox(height: SimfTokens.space4),
                  // Frame 922:2824 — three bands: the top tier is the gold hero
                  // card, the lowest tier is a compact logo-tile grid, and any tier
                  // in between is a navy premium card (position-based so it is
                  // faithful for any tier naming, not just Platinum/Gold/Silver).
                  if (i == lastIndex && visibleGroups.length > 1)
                    SponsorGrid(
                      sponsors: visibleGroups[i].sponsors,
                      baseUrl: baseUrl,
                      isArabic: isArabic,
                    )
                  else
                    for (final sponsor
                        in visibleGroups[i].sponsors) ...<Widget>[
                      SponsorCard(
                        id: sponsor.id,
                        baseUrl: baseUrl,
                        name: sponsor.localizedName(isArabic: isArabic),
                        badge: sponsorBadgeText(sponsor, isArabic: isArabic),
                        // D-432 — prefer the authored tagline (Figma's "الراعي
                        // الاستراتيجي · …" line); fall back to the website link.
                        secondary:
                            sponsor.localizedTagline(isArabic: isArabic) ??
                                sponsor.url,
                        hero: i == 0,
                        // Wave 3 — tap → the sponsor detail (Figma 1439:11826).
                        onTap: () => context.pushNamed(
                          RouteNames.sponsorDetail,
                          pathParameters: <String, String>{
                            RouteParams.sponsorId: sponsor.id,
                          },
                        ),
                      ),
                      const SizedBox(height: SimfTokens.space4),
                    ],
                ],
              ],
            ),
          );
        },
      ),
    );
  }
}
