import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The no-logo / failed-fetch day-banner fall-back: a navy box with the anchor
/// glyph (the designed empty state until a day logo is uploaded).
class DayBannerFallback extends StatelessWidget {
  const DayBannerFallback({super.key});

  @override
  Widget build(BuildContext context) => const ColoredBox(
        color: SimfTokens.navy,
        child: Center(
          child: Icon(
            Icons.image_outlined,
            size: SimfTokens.dayBannerFallbackSize,
            color: SimfTokens.beigeBorder,
          ),
        ),
      );
}
