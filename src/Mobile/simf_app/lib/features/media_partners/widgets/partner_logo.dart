import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_logo_image.dart';
import '../data/media_partner_models.dart';
import 'initials_tile.dart';

class PartnerLogo extends StatelessWidget {
  const PartnerLogo({required this.url, required this.name});

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
                width: SimfTokens.partnerCardWidth,
                height: SimfTokens.partnerCardHeight,
                child: CircularProgressIndicator(strokeWidth: SimfTokens.partnerCardStrokeWidth),
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
