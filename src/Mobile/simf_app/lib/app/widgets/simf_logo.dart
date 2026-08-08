import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/app_assets.dart';

/// The SIMF compass/palm/anchor brand mark — a white mark on a transparent
/// square, bundled once at 4x (544 px) so it renders crisp from the 44 px
/// sign-in header up to the 136 px splash hero (D-359). Icon art refreshed
/// 2026-07-13 to match the launcher icon (`icon/app_icon*.png`); the asset stays
/// transparent (no navy field) so it composites onto the navy screens.
class SimfLogo extends StatelessWidget {
  const SimfLogo({required this.size, super.key});

  /// Rendered width and height in logical pixels (the mark is square).
  final double size;

  @override
  Widget build(BuildContext context) {
    return Image.asset(
      AppAssets.simfLogo,
      width: size,
      height: size,
    );
  }
}
