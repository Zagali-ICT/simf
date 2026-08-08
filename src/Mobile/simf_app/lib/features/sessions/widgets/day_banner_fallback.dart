import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';

/// The no-logo / failed-fetch day-banner fall-back: a navy box with the anchor
/// glyph (the designed empty state until a day logo is uploaded).
class DayBannerFallback extends StatelessWidget {
  const DayBannerFallback();

  @override
  Widget build(BuildContext context) => const ColoredBox(
        color: SimfTokens.navy,
        child: Center(
          child: Icon(
            Icons.image_outlined,
            size: 28,
            color: SimfTokens.beigeBorder,
          ),
        ),
      );
}
