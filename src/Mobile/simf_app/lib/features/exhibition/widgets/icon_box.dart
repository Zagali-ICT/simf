import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';

/// The 44×44 beige-fill icon box (Figma 1439:11913 / 11926): beige-10% fill,
/// beige hairline, radius-4, with a 20px gold glyph centred. A bundled Figma
/// SVG ([iconAsset]) takes precedence over the Material [icon] when supplied.
class IconBox extends StatelessWidget {
  const IconBox({required this.icon, this.iconAsset});

  final IconData icon;
  final String? iconAsset;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: SimfTokens.iconBoxWidth,
      height: SimfTokens.iconBoxHeight,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.beigeFill10,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall), // 4
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: iconAsset == null
          ? Icon(icon, size: SimfTokens.iconBoxSize, color: SimfTokens.accent)
          : SimfSvgIcon(iconAsset!, size: SimfTokens.iconBoxSize, color: SimfTokens.accent),
    );
  }
}
