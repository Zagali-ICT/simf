import 'package:flutter/material.dart';

import '../../core/country_flag.dart';
import '../theme/tokens.dart';

/// Overlays a small **country flag** badge on the **top-left corner** of [child]
/// (a logo / avatar), derived from [countryId] (ISO 3166-1 numeric). When the
/// country is null or unknown the child is returned unchanged (no badge, never a
/// tofu box). Shared by the speaker / sponsor / booth / archive cards (D-456) so
/// the badge chrome has one owner.
class CountryFlagBadge extends StatelessWidget {
  const CountryFlagBadge({
    required this.child,
    required this.countryId,
    this.size = 12,
    super.key,
  });

  final Widget child;
  final int? countryId;

  /// The flag glyph font-size (the badge sizes to it).
  final double size;

  @override
  Widget build(BuildContext context) {
    final flag = countryFlagEmoji(countryId);
    if (flag == null) {
      return child;
    }
    return Stack(
      clipBehavior: Clip.none,
      children: <Widget>[
        child,
        Positioned(
          top: -2,
          left: -2,
          child: Container(
            padding: const EdgeInsets.all(1),
            decoration: BoxDecoration(
              color: SimfTokens.navy,
              shape: BoxShape.circle,
              border: Border.all(color: SimfTokens.navyDeep, width: 0.5),
            ),
            child: Text(
              flag,
              textDirection: TextDirection.ltr,
              style: TextStyle(fontSize: size, height: 1),
            ),
          ),
        ),
      ],
    );
  }
}
