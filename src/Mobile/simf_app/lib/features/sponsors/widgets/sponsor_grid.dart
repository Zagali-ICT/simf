import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../app/route_names.dart';
import '../../../app/theme/tokens.dart';
import '../data/sponsor_models.dart';
import 'sponsor_grid_tile.dart';
import 'sponsor_logo.dart';

/// The lowest-tier band rendered as the frame's compact 3-column logo grid
/// (frame 922:2824 "رعاة ذهبيون"): each tile is the sponsor's logo over its
/// name. Non-scrolling (it lives inside the page's ListView).
class SponsorGrid extends StatelessWidget {
  const SponsorGrid({
    required this.sponsors,
    required this.baseUrl,
    required this.isArabic,
    super.key,
  });

  final List<Sponsor> sponsors;
  final String baseUrl;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    // Frame 922:2824 (925:3030..) — 3 columns, 16px row gap, 8px column gap,
    // each tile a fixed 72-high card.
    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      padding: EdgeInsets.zero,
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 3,
        mainAxisSpacing: SimfTokens.space4,
        crossAxisSpacing: SimfTokens.space2,
        mainAxisExtent: SimfTokens.sponsorRowHeight,
      ),
      itemCount: sponsors.length,
      itemBuilder: (context, i) => SponsorGridTile(
        id: sponsors[i].id,
        baseUrl: baseUrl,
        name: sponsors[i].localizedName(isArabic),
        initials: sponsorBadgeText(sponsors[i], isArabic),
        // Wave 3 — tap → the sponsor detail (Figma 1439:11826).
        onTap: () => context.pushNamed(
          RouteNames.sponsorDetail,
          pathParameters: <String, String>{RouteParams.sponsorId: sponsors[i].id},
        ),
      ),
    );
  }
}

