import 'package:flutter/material.dart';

import '../../app/theme/tokens.dart';

/// The square logo on an exhibitor / sponsor detail card (Figma 1439:11881 /
/// 11826): the real ExhibitorLogo / SponsorLogo asset (served anonymously per
/// D-357) clipped to fill, falling back to the entity initials while it loads or
/// when no logo is set (the asset route 404s) or [url] is null.
///
/// [fallbackUrl] is an optional second logo tried when [url] 404s / is null —
/// the exhibitor detail passes its own ExhibitorLogo as [url] and the legacy
/// Contact CompanyLogo as [fallbackUrl], so an exhibitor that has not yet
/// re-uploaded its own logo still shows its company logo instead of initials.
class EntityLogoImage extends StatelessWidget {
  const EntityLogoImage({
    required this.url,
    required this.initials,
    this.fallbackUrl,
    super.key,
  });

  final String? url;
  final String? fallbackUrl;
  final String initials;

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
      child: _network(
        url,
        placeholder: fallback,
        onError: () => _network(
          fallbackUrl,
          placeholder: fallback,
          onError: () => fallback,
        ),
      ),
    );
  }

  // Renders [src] as a network image showing [placeholder] while it loads and
  // calling [onError] when it fails / is null — so callers can chain a second
  // URL before the initials tile, without fetching it unless the first fails.
  Widget _network(
    String? src, {
    required Widget placeholder,
    required Widget Function() onError,
  }) {
    if (src == null || src.isEmpty) {
      return onError();
    }
    return Image.network(
      src,
      fit: BoxFit.cover,
      gaplessPlayback: true,
      loadingBuilder: (context, child, progress) =>
          progress == null ? child : placeholder,
      errorBuilder: (context, error, stackTrace) => onError(),
    );
  }
}
