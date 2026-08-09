import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sessions/widgets/day_banner_fallback.dart';

/// The day banner (frame node 1064:13240): the selected day's logo image under a
/// navy bottom-gradient with the gold anchor badge at the inline-end. A navy
/// anchor-glyph box is the no-logo fall-back. 85 high, full width.
class ProgrammeDayBanner extends StatelessWidget {
  const ProgrammeDayBanner({this.imageUrl, super.key});

  final String? imageUrl;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: SimfTokens.dayBannerHeight,
      width: double.infinity,
      child: ClipRRect(
        borderRadius:
            const BorderRadius.all(Radius.circular(SimfTokens.radius)),
        child: Stack(
          fit: StackFit.expand,
          children: <Widget>[
            if (imageUrl != null)
              Image.network(
                imageUrl!,
                fit: BoxFit.cover,
                gaplessPlayback: true,
                loadingBuilder: (context, child, progress) => progress == null
                    ? child
                    : const ColoredBox(color: SimfTokens.navy),
                errorBuilder: (context, error, stackTrace) =>
                    const DayBannerFallback(),
              )
            else
              const DayBannerFallback(),
            // The frame's bottom gradient (transparent → navy #001030 @ 80%).
            const DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.center,
                  end: Alignment.bottomCenter,
                  colors: <Color>[
                    SimfTokens.transparent,
                    SimfTokens.bannerScrim,
                  ],
                ),
              ),
            ),
            // The gold anchor badge (frame 1064:13249) — inline-start (physical
            // right under RTL), matching the frame.
            PositionedDirectional(
              top: SimfTokens.space2,
              start: SimfTokens.space2,
              child: Container(
                width: SimfTokens.requestIconBox,
                height: SimfTokens.requestIconBox,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: SimfTokens.accent,
                  borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                ),
                child: const Icon(
                  Icons.anchor,
                  size: SimfTokens.programmeDayBannerSize,
                  color: SimfTokens.navy,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

