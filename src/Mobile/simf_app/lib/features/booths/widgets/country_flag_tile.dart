import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// The booth country flag tile (frame node 1062:12911): a 40×40 rounded tile at
/// the inline-end (left) of the header. Figma renders a full flag image; the
/// app renders the country **emoji** (its only flag form), centred on the navy
/// tile.
class CountryFlagTile extends StatelessWidget {
  const CountryFlagTile({required this.flag, super.key});

  final String flag;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: SimfTokens.space10,
      height: SimfTokens.space10,
      alignment: Alignment.center,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        flag,
        textDirection: TextDirection.ltr,
        style: const TextStyle(
            fontSize: SimfTokens.countryFlagTileFontSize, height: 1,),
      ),
    );
  }
}
