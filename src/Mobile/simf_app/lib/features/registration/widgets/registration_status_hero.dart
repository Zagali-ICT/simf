import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';

/// The status hero (Figma 1701:3799–3804): a 104px ring (navyDeep fill, a
/// state-coloured 2.36px border) around the state icon, a white headline, and a
/// beige secondary message. The ring + icon carry the state [color]; the
/// headline is always white.
class RegistrationStatusHero extends StatelessWidget {
  const RegistrationStatusHero({
    required this.color,
    required this.icon,
    required this.headline,
    required this.message,
    super.key,
  });

  final Color color;
  final IconData icon;
  final String headline;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: <Widget>[
        Container(
          width: SimfTokens.registrationStatusHeroWidthMd,
          height: SimfTokens.registrationStatusHeroHeightMd,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: SimfTokens.navyDeep,
            shape: BoxShape.circle,
            border: Border.all(color: color, width: SimfTokens.registrationStatusHeroWidthSm),
          ),
          child: Icon(icon, size: SimfTokens.registrationStatusHeroSize, color: color),
        ),
        const SizedBox(height: SimfTokens.space4),
        Text(
          headline,
          textAlign: TextAlign.center,
          style: const TextStyle(
            fontSize: SimfTokens.text24, // 24
            fontWeight: FontWeight.w700,
            color: SimfTokens.surface,
          ),
        ),
        const SizedBox(height: SimfTokens.space4),
        Text(
          message,
          textAlign: TextAlign.center,
          style: const TextStyle(
            color: SimfTokens.beigeBorder,
            fontSize: SimfTokens.textMd, // 14
            height: SimfTokens.registrationStatusHeroHeightSm,
          ),
        ),
      ],
    );
  }
}
