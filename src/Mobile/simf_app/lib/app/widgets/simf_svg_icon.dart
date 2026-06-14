import 'package:flutter/widgets.dart';
import 'package:flutter_svg/flutter_svg.dart';

/// A bundled SVG glyph tinted to [color] (via `srcIn`) — the single shared form
/// for the KSA frames' exact iconify / Figma icons.
///
/// The design uses icon sets (solar / ph / iconamoon / mingcute / …) that have
/// no 1:1 Material equivalent, so they ship as bundled SVGs under
/// `assets/icons/` and are recoloured to the target colour here. Every screen
/// renders them through this one widget so none re-implements the
/// `SvgPicture.asset` + `ColorFilter.mode(..., BlendMode.srcIn)` boilerplate.
class SimfSvgIcon extends StatelessWidget {
  const SimfSvgIcon(
    this.asset, {
    required this.size,
    required this.color,
    super.key,
  });

  /// Asset path, e.g. `assets/icons/auth_globe.svg`.
  final String asset;
  final double size;
  final Color color;

  @override
  Widget build(BuildContext context) => SvgPicture.asset(
        asset,
        width: size,
        height: size,
        colorFilter: ColorFilter.mode(color, BlendMode.srcIn),
      );
}
