import 'package:flutter/material.dart';
import '../../../app/theme/tokens.dart';
import 'sponsor_logo.dart';

/// The square logo chip on a sponsor card — frame's 53×53 box. On the gold hero
/// card it is gold-filled with a navy edge; on a navy premium card it is
/// navy-filled with a gold edge. Hosts the real [SponsorLogo] (clipped to fill),
/// falling back to the acronym initials.
class BadgeBox extends StatelessWidget {
  const BadgeBox({required this.child, required this.hero});

  final Widget child;
  final bool hero;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 53,
      height: 53,
      clipBehavior: Clip.antiAlias,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: hero ? SimfTokens.accent : SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: hero ? SimfTokens.navy : SimfTokens.accent,
          width: SimfTokens.hairline,
        ),
      ),
      child: child,
    );
  }
}
