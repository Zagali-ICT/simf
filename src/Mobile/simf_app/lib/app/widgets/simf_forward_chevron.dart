import 'package:flutter/widgets.dart';

import 'package:simf_app/app/widgets/simf_svg_icon.dart';

/// A forward-navigation chevron — the "tap to open" caret at a list row's
/// inline end.
///
/// The bundled chevron assets (`ic_back.svg`, `ic_caret_left.svg`, …) draw a
/// LEFT-pointing glyph, which is correct at the inline end in RTL (Arabic).
/// But [SimfSvgIcon] never mirrors, so in LTR the same glyph still points
/// left — backwards for the reading direction. This wraps it in a
/// [Transform.flip] that mirrors it horizontally in LTR only, so the caret
/// points right (forward) in English and left (forward) in Arabic. Callers
/// keep their exact asset, size and colour.
class SimfForwardChevron extends StatelessWidget {
  const SimfForwardChevron(
    this.asset, {
    required this.size,
    required this.color,
    super.key,
  });

  /// Asset path of a LEFT-pointing chevron / caret (e.g. `ic_back.svg`).
  final String asset;
  final double size;
  final Color color;

  @override
  Widget build(BuildContext context) {
    // The asset points left (forward at the inline end in RTL); mirror it
    // in LTR so it points right — forward in English reading order.
    final flipForLtr = Directionality.of(context) == TextDirection.ltr;
    return Transform.flip(
      flipX: flipForLtr,
      child: SimfSvgIcon(asset, size: size, color: color),
    );
  }
}
