import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/sponsors/data/sponsor_models.dart';
import 'package:simf_app/features/sponsors/widgets/sponsor_card.dart';
import 'package:simf_app/features/sponsors/widgets/sponsor_grid.dart';
import 'package:simf_app/features/sponsors/widgets/sponsor_logo.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The sponsors page body: one section per non-empty tier, in the order the API
/// returns them, or the empty state when no tier carries a sponsor.
class SponsorTierList extends ConsumerWidget {
  const SponsorTierList({
    required this.groups,
    required this.onRefresh,
    super.key,
  });

  final List<SponsorTierGroup> groups;
  final Future<void> Function() onRefresh;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final visibleGroups = visibleTiers(groups);
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
            // card, the lowest tier is a compact logo-tile grid, and any
            // tier in between is a navy premium card (position-based so
            // it is faithful for any tier naming, not just
            // Platinum/Gold/Silver).
            if (i == lastIndex && visibleGroups.length > 1)
              SponsorGrid(
                sponsors: visibleGroups[i].sponsors,
                baseUrl: baseUrl,
                isArabic: isArabic,
              )
            else
              for (final sponsor in visibleGroups[i].sponsors) ...<Widget>[
                SponsorCard(
                  id: sponsor.id,
                  baseUrl: baseUrl,
                  name: sponsor.localizedName(isArabic: isArabic),
                  badge: sponsorBadgeText(sponsor, isArabic: isArabic),
                  // D-432 — prefer the authored tagline (Figma's "الراعي
                  // الاستراتيجي · …" line); fall back to the website
                  // link.
                  secondary: sponsor.localizedTagline(isArabic: isArabic) ??
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
  }
}
