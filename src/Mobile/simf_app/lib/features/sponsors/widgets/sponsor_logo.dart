import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../data/sponsor_models.dart';

/// The sponsor's real logo (D-357 `SponsorLogo` asset, served anonymously at
/// `{base}/app/assets/SponsorLogo/{id}/image`) clipped to fill its parent box,
/// falling back to the acronym initials while it loads or when no logo is set
/// (the route 404s). [hero] picks the initials colour for the box it sits in.
class SponsorLogo extends StatelessWidget {
  const SponsorLogo({
    required this.id,
    required this.baseUrl,
    required this.fallbackInitials,
    required this.hero,
    super.key,
  });

  final String id;
  final String baseUrl;
  final String fallbackInitials;
  final bool hero;

  @override
  Widget build(BuildContext context) {
    final fallback = Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space1),
        child: Text(
          fallbackInitials,
          textAlign: TextAlign.center,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: hero
              ? SimfTokens.labelNavySemibold
              : SimfTokens.labelWhiteSemibold,
        ),
      ),
    );
    if (id.isEmpty) {
      return fallback;
    }
    return Image.network(
      '$baseUrl/app/assets/SponsorLogo/$id/image',
      fit: BoxFit.fill,
      width: double.infinity,
      height: double.infinity,
      gaplessPlayback: true,
      loadingBuilder: (context, child, progress) =>
          progress == null ? child : fallback,
      errorBuilder: (context, error, stackTrace) => fallback,
    );
  }
}

/// The short identifier shown in a sponsor's square badge box (the frame's
/// "SAMI" / "GAMI" chip). The API has no acronym field, so derive initials from
/// the localized name — the same interim logo-as-initials treatment the badge
/// strip uses elsewhere.
String sponsorBadgeText(Sponsor sponsor, bool isArabic) {
  final name = sponsor.localizedName(isArabic);
  final words = name.trim().split(RegExp(r'\s+'));
  final letters = words
      .where((w) => w.isNotEmpty)
      .take(2)
      .map((w) => w.characters.first)
      .join();
  return letters.isEmpty ? '—' : letters.toUpperCase();
}
