import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';
import 'package:simf_app/features/sponsors/widgets/badge_box.dart';
import 'package:simf_app/features/sponsors/widgets/sponsor_logo.dart';

/// One sponsor card — frame 922:2824's 72-high row. RTL puts the square logo
/// badge on the inline-start (physical right), the name + secondary line next
/// to it, and the forward chevron on the far inline-end (physical left).
///
/// [hero] true is the strategic gold card (gold fill, dark text, gold initials
/// badge with a navy edge, navy chevron); false is the premium navy card
/// (navyDeep fill, beige hairline, white text, navy initials badge with a gold
/// edge, gold chevron).
class SponsorCard extends StatelessWidget {
  const SponsorCard({
    required this.id,
    required this.baseUrl,
    required this.name,
    required this.badge,
    required this.secondary,
    required this.hero,
    required this.onTap,
    super.key,
  });

  final String id;
  final String baseUrl;
  final String name;
  final String badge;
  final String? secondary;
  final bool hero;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // Frame 922:2824 — the sponsor name is white on BOTH the gold hero card
    // (925:2979 `text-white`) and the navy premium card; only the secondary
    // line changes colour with the card.
    final subColor = hero ? SimfTokens.navyDeep : SimfTokens.beigeBorder;
    final flip = !AppL10n.of(context).isArabic;
    return SimfCard(
      onTap: onTap,
      color: hero ? SimfTokens.accent : SimfTokens.navyDeep,
      child: ConstrainedBox(
        constraints: const BoxConstraints(
          minHeight: SimfTokens.sponsorRowHeight,
        ),
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2),
          child: Row(
            children: <Widget>[
              // Frame 922:2824 — RTL order: the square logo badge sits at the
              // inline-start (physical right), the name + secondary line next
              // to it, and the forward chevron on the far inline-end (physical
              // left). The bundled caret does not auto-mirror, so it keeps
              // pointing left as the design shows.
              BadgeBox(
                hero: hero,
                child: SponsorLogo(
                  id: id,
                  baseUrl: baseUrl,
                  fallbackInitials: badge,
                  hero: hero,
                  name: name,
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              Expanded(
                child: Column(
                  // Figma 925:2978 — items-end / text-right. In RTL,
                  // CrossAxisAlignment.start is the physical RIGHT edge, so the
                  // name + tagline hug the badge. The tagline is capped to the
                  // frame's ~215px (ConstrainedBox) so its box has a definite
                  // width and textAlign.right keeps EVERY wrapped line flush to
                  // the badge (a free-width Text left short last lines
                  // centred).
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisAlignment: MainAxisAlignment.center,
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Text(
                      name,
                      textAlign: TextAlign.start,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: SimfTokens.labelWhiteBold,
                    ),
                    if (secondary != null &&
                        secondary!.trim().isNotEmpty) ...<Widget>[
                      const SizedBox(height: SimfTokens.space1),
                      // Responsive (owner 2026-06-28): fill the available
                      // column width instead of the frame's fixed 215px so the
                      // tagline stretches on a tablet; textAlign.right keeps
                      // the wrapped lines flush to the badge.
                      SizedBox(
                        width: double.infinity,
                        child: Text(
                          secondary!,
                          textAlign: TextAlign.start,
                          style: SimfTokens.bodySm.copyWith(color: subColor),
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              // The bundled SVG does not auto-mirror under RTL, so flip
              // horizontally in English so the caret points forward (→ in LTR).
              Transform.flip(
                flipX: flip,
                child: SimfSvgIcon(
                  // Frame 925:2990 — the iconamoon thin chevron (navy on the
                  // gold hero card, gold on a premium card), NOT a filled
                  // triangle.
                  AppAssets.icBack,
                  size: SimfTokens.sponsorCardSize,
                  color: hero ? SimfTokens.navy : SimfTokens.accent,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
