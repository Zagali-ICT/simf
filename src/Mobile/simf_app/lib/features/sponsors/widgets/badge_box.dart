import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sponsors/widgets/sponsor_logo.dart';

/// The square logo chip on a sponsor card — frame's 53×53 box. On the gold hero
/// card it is gold-filled with a navy edge; on a navy premium card it is
/// navy-filled with a gold edge. Hosts the real [SponsorLogo] (clipped to
/// fill), falling back to the acronym initials.
class BadgeBox extends StatelessWidget {
  const BadgeBox({required this.child, required this.hero, super.key});

  final Widget child;
  final bool hero;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: SimfTokens.badgeBoxWidth,
      height: SimfTokens.badgeBoxHeight,
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
