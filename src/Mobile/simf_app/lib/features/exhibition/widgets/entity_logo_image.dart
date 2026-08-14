import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_logo_image.dart';

/// The square logo on an exhibitor / sponsor detail card (Figma 1439:11881 /
/// 11826): the real ExhibitorLogo / SponsorLogo asset (served anonymously per
/// D-357) shown **whole** inside the box, falling back to the entity initials
/// while it loads or when no logo is set (the asset route 404s) or [url] is
/// null.
///
/// [fallbackUrl] is an optional second logo tried when [url] 404s / is null —
/// the exhibitor detail passes its own ExhibitorLogo as [url] and the legacy
/// Contact CompanyLogo as [fallbackUrl], so an exhibitor that has not yet
/// re-uploaded its own logo still shows its company logo instead of initials.
///
/// Owner 2026-07-26 — a brand mark must FIT its box, so this renders through
/// the shared [SimfLogoImage] with its `BoxFit.contain` default (the old
/// `BoxFit.cover` cropped wide logos); tapping it opens the logo full size.
class EntityLogoImage extends StatelessWidget {
  const EntityLogoImage({
    required this.url,
    required this.initials,
    required this.name,
    this.fallbackUrl,
    super.key,
  });

  final String? url;
  final String? fallbackUrl;
  final String initials;

  /// The exhibitor / sponsor name — the picture's accessible name and the
  /// full-size viewer's title.
  final String name;

  @override
  Widget build(BuildContext context) {
    final fallback = Center(
      child: Text(
        initials,
        textDirection: TextDirection.ltr,
        style: SimfTokens.labelWhiteBoldXl,
      ),
    );
    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      // Primary logo → fallback logo → initials tile (each on error / null).
      // The second URL is only fetched when the first one fails.
      child: SimfLogoImage(
        url: url,
        placeholder: fallback,
        semanticLabel: name,
        onError: () => SimfLogoImage(
          url: fallbackUrl,
          placeholder: fallback,
          semanticLabel: name,
          onError: () => fallback,
        ),
      ),
    );
  }
}
