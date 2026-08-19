import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_logo_image.dart';
import 'package:simf_app/core/utils/initials.dart';
import 'package:simf_app/features/media_partners/widgets/initials_tile.dart';

/// The gold rounded-square logo holder (frame node 958:2264). Renders the
/// partner's uploaded logo from the public anonymous asset route with a spinner
/// while it loads; falls back to the partner's initials on a gold tile when the
/// partner has no logo (the route 404s) or the fetch fails.
///
/// Owner 2026-07-26 — the mark FITS the tile (`BoxFit.contain` via the shared
/// [SimfLogoImage]; the old `BoxFit.cover` cropped wide mastheads). The
/// press-to-enlarge lives on the whole CARD (FR-LGO-003), so the box itself
/// does not claim the tap — one gesture, one target, no nested handlers.
class PartnerLogo extends StatelessWidget {
  const PartnerLogo({required this.url, required this.name, super.key});

  final String url;
  final String name;

  static const double _size = 48;

  String get _initials => initialsFromWords(name);

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
                width: SimfTokens.partnerCardWidth,
                height: SimfTokens.partnerCardHeight,
                child: CircularProgressIndicator(
                    strokeWidth: SimfTokens.partnerCardStrokeWidth,),
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
