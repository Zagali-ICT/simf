import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_logo_image.dart';
import '../../../app/widgets/simf_page_shell.dart';

/// One partner — frame node 958:2263: the navy KSA card with a centred gold
/// rounded-square logo holder over the partner name (white 12px SemiBold).
class PartnerCard extends StatelessWidget {
  const PartnerCard({required this.name, required this.logoUrl, super.key});

  final String name;
  final String logoUrl;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            _PartnerLogo(url: logoUrl, name: name),
            const SizedBox(height: SimfTokens.space2),
            // Flexible so a long Arabic name (or a large OS text-scale) shrinks
            // + ellipsises inside the fixed-aspect grid cell instead of
            // overflowing the column.
            Flexible(
              child: Text(
                name,
                textAlign: TextAlign.center,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: SimfTokens.labelWhiteSemiboldSm13,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// The gold rounded-square logo holder (frame node 958:2264). Renders the
/// partner's uploaded logo from the public anonymous asset route with a spinner
/// while it loads; falls back to the partner's initials on a gold tile when the
/// partner has no logo (the route 404s) or the fetch fails.
///
/// Owner 2026-07-26 — the mark FITS the tile (`BoxFit.contain` via the shared
/// [SimfLogoImage]; the old `BoxFit.cover` cropped wide mastheads) and opens
/// full size on tap.
class _PartnerLogo extends StatelessWidget {
  const _PartnerLogo({required this.url, required this.name});

  final String url;
  final String name;

  static const double _size = 48;
  static final RegExp _whitespace = RegExp(r'\s+');

  String get _initials {
    final words = name.trim().split(_whitespace);
    final letters = words
        .where((w) => w.isNotEmpty)
        .take(2)
        .map((w) => w.characters.first)
        .join();
    return letters.isEmpty ? '—' : letters.toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius:
          const BorderRadius.all(Radius.circular(SimfTokens.radiusSmall)),
      child: SizedBox(
        width: _size,
        height: _size,
        child: SimfLogoImage(
          url: url,
          semanticLabel: name,
          placeholder: const ColoredBox(
            color: SimfTokens.navyDeep,
            child: Center(
              child: SizedBox(
                width: 18,
                height: 18,
                child: CircularProgressIndicator(strokeWidth: 2),
              ),
            ),
          ),
          // Initials are computed only when the fetch fails — the common
          // success path skips the split.
          onError: () => _InitialsTile(initials: _initials),
        ),
      ),
    );
  }
}

/// The no-logo / failed-fetch fall-back: the partner's initials on the frame's
/// gold tile (navy text for contrast on gold).
class _InitialsTile extends StatelessWidget {
  const _InitialsTile({required this.initials});

  final String initials;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: SimfTokens.accent,
      child: Center(
        child: Text(
          initials,
          style: SimfTokens.labelNavyBoldTracked,
        ),
      ),
    );
  }
}
