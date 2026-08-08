import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_logo_image.dart';
import '../../../core/net/asset_urls.dart';
import '../data/sponsor_models.dart';

/// The sponsor's real logo (D-357 `SponsorLogo` asset, served anonymously at
/// `{base}/app/assets/SponsorLogo/{id}/image`) shown **whole** inside its parent
/// box, falling back to the acronym initials while it loads or when no logo is
/// set (the route 404s). [hero] picks the initials colour for the box it sits in.
///
/// Owner 2026-07-26 — a sponsor mark must FIT its box (the old `BoxFit.cover`
/// cropped wide logos), so it renders through the shared [SimfLogoImage]. Set
/// [enableFullScreen] only where the logo is not inside a tappable row — a card
/// / grid cell owns its own tap (it opens the sponsor detail, whose 108px
/// identity logo IS tappable to full size).
class SponsorLogo extends StatelessWidget {
  const SponsorLogo({
    required this.id,
    required this.baseUrl,
    required this.fallbackInitials,
    required this.hero,
    required this.name,
    this.enableFullScreen = false,
    super.key,
  });

  final String id;
  final String baseUrl;
  final String fallbackInitials;
  final bool hero;

  /// The sponsor's localized name — the logo's accessible name and the
  /// full-size viewer's title.
  final String name;

  /// Opens the logo full size on tap. Off by default: every current call site
  /// is a tappable card / grid cell that navigates instead.
  final bool enableFullScreen;

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
    // MERGE 2026-07-28 — main and this branch fixed the SAME complaint (wide
    // sponsor logos rendering badly) in incompatible ways. main went
    // BoxFit.cover -> BoxFit.fill, which stops the cropping but STRETCHES the
    // mark: a sponsor's logo is a brand asset and distorting it is worse than
    // the cropping it replaced. This branch's fix routes through the shared
    // SimfLogoImage, which shows the mark whole, per the Owner 2026-07-26
    // decision recorded above, and also carries the accessible name and the
    // tap-to-full-size affordance. Kept this side.
    return SimfLogoImage(
      url: AssetUrls.image(baseUrl, AssetKind.sponsorLogo, id),
      placeholder: fallback,
      semanticLabel: name,
      width: double.infinity,
      height: double.infinity,
      enableFullScreen: enableFullScreen,
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
