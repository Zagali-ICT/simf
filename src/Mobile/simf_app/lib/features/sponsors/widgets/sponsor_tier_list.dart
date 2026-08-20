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
    // Flattened to one row per line so the directory builds lazily. A header
    // row is the tier heading; a row with no sponsor and no header flag is the
    // lowest tier's logo grid, which stands in for that tier's cards. Each row
    // carries the gaps that used to be sibling spacers, so the spacing is the
    // same widget tree with the gap moved into the padding.
    final rows = <({int group, Sponsor? sponsor, bool header})>[];
    for (var i = 0; i < visibleGroups.length; i++) {
      rows.add((group: i, sponsor: null, header: true));
      // Frame 922:2824 — three bands: the top tier is the gold hero card, the
      // lowest tier is a compact logo-tile grid, and any tier in between is a
      // navy premium card (position-based so it is faithful for any tier
      // naming, not just Platinum/Gold/Silver).
      if (i == lastIndex && visibleGroups.length > 1) {
        rows.add((group: i, sponsor: null, header: false));
        continue;
      }
      for (final sponsor in visibleGroups[i].sponsors) {
        rows.add((group: i, sponsor: sponsor, header: false));
      }
    }
    return SimfPullToRefresh(
      onRefresh: onRefresh,
      child: ListView.builder(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(SimfTokens.space4),
        itemCount: rows.length,
        itemBuilder: (context, index) {
          final row = rows[index];
          final group = visibleGroups[row.group];
          if (row.header) {
            return Padding(
              // Every tier but the first opens with a 24px gap above its
              // heading, and every heading is followed by a 16px one.
              padding: EdgeInsets.only(
                top: row.group > 0 ? SimfTokens.space6 : 0,
                bottom: SimfTokens.space4,
              ),
              child: SimfSectionHeader(
                title: l10n.sponsorTierLabel(group.tier, group.tierName),
              ),
            );
          }
          final sponsor = row.sponsor;
          if (sponsor == null) {
            return SponsorGrid(
              sponsors: group.sponsors,
              baseUrl: baseUrl,
              isArabic: isArabic,
            );
          }
          return Padding(
            padding: const EdgeInsets.only(bottom: SimfTokens.space4),
            child: SponsorCard(
              key: ValueKey<String>(sponsor.id),
              id: sponsor.id,
              baseUrl: baseUrl,
              name: sponsor.localizedName(isArabic: isArabic),
              badge: sponsorBadgeText(sponsor, isArabic: isArabic),
              // D-432 — prefer the authored tagline (Figma's "الراعي
              // الاستراتيجي · …" line); fall back to the website link.
              secondary:
                  sponsor.localizedTagline(isArabic: isArabic) ?? sponsor.url,
              hero: row.group == 0,
              // Wave 3 — tap → the sponsor detail (Figma 1439:11826).
              onTap: () => context.pushNamed(
                RouteNames.sponsorDetail,
                pathParameters: <String, String>{
                  RouteParams.sponsorId: sponsor.id,
                },
              ),
            ),
          );
        },
      ),
    );
  }
}
