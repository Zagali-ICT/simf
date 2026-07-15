import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../app/route_names.dart';
import '../../../app/theme/tokens.dart';
import '../data/sponsor_models.dart';
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
        mainAxisExtent: 72,
      ),
      itemCount: sponsors.length,
      itemBuilder: (context, i) => _SponsorGridTile(
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

/// One gold-tier grid tile (frame 925:3031): a single 72-high navy card on the
/// beige hairline holding the sponsor logo above its name (12px SemiBold white,
/// centred). The logo fills the area above the name; initials are the fallback.
class _SponsorGridTile extends StatelessWidget {
  const _SponsorGridTile({
    required this.id,
    required this.baseUrl,
    required this.name,
    required this.initials,
    required this.onTap,
  });

  final String id;
  final String baseUrl;
  final String name;
  final String initials;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.navyDeep,
      clipBehavior: Clip.antiAlias,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: const BorderSide(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Expanded(
                child: SponsorLogo(
                  id: id,
                  baseUrl: baseUrl,
                  fallbackInitials: initials,
                  hero: false,
                ),
              ),
              const SizedBox(height: SimfTokens.space2),
              Text(
                name,
                textAlign: TextAlign.center,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: Colors.white,
                  fontWeight: FontWeight.w600,
                  fontSize: SimfTokens.textSm,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
