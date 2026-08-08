import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_image_viewer.dart';
import '../../../app/widgets/simf_logo_image.dart';
import '../../../app/widgets/simf_page_shell.dart';
import 'initials_tile.dart';

/// One partner — frame node 958:2263: the navy KSA card with a centred gold
/// rounded-square logo holder over the partner name (white 12px SemiBold).
///
/// FR-LGO-003 — the WHOLE card is the tap target, not just the 48px logo box:
/// pressing anywhere on it opens the partner's mark full size in the shared
/// [SimfImageViewer], the same press-to-enlarge affordance the sponsor and
/// exhibitor surfaces carry. Media partners have no detail route (the frame
/// defines none), so the card used to be completely inert.
class PartnerCard extends StatelessWidget {
  const PartnerCard({required this.name, required this.logoUrl, super.key});

  final String name;
  final String logoUrl;

  @override
  Widget build(BuildContext context) {
    final url = logoUrl.trim();
    final Widget body = SimfCard(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            _PartnerLogo(url: url, name: name),
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
    // A partner with no logo has nothing to enlarge — it keeps the plain card
    // rather than advertising a press that would open an initials tile.
    if (url.isEmpty) {
      return body;
    }
    return SimfTapToEnlarge(
      image: NetworkImage(url),
      label: name,
      child: body,
    );
  }
}

/// The gold rounded-square logo holder (frame node 958:2264). Renders the
/// partner's uploaded logo from the public anonymous asset route with a spinner
/// while it loads; falls back to the partner's initials on a gold tile when the
/// partner has no logo (the route 404s) or the fetch fails.
///
/// Owner 2026-07-26 — the mark FITS the tile (`BoxFit.contain` via the shared
/// [SimfLogoImage]; the old `BoxFit.cover` cropped wide mastheads). The
/// press-to-enlarge lives on the whole CARD (FR-LGO-003), so the box itself
/// does not claim the tap — one gesture, one target, no nested handlers.
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
          onError: () => InitialsTile(initials: _initials),
          enableFullScreen: false,
        ),
      ),
    );
  }
}

