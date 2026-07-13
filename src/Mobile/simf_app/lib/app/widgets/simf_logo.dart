import 'package:flutter/material.dart';

/// The SIMF compass/palm/anchor brand mark — a white mark on a transparent
/// square, bundled once at 4x (544 px) so it renders crisp from the 44 px
/// sign-in header up to the 136 px splash hero (D-359). Icon art refreshed
/// 2026-07-13 to match the launcher icon (`icon/app_icon*.png`); the asset stays
/// transparent (no navy field) so it composites onto the navy screens.
class SimfLogo extends StatelessWidget {
  const SimfLogo({super.key, required this.size});

  /// Rendered width and height in logical pixels (the mark is square).
  final double size;

  @override
  Widget build(BuildContext context) {
    return Image.asset(
      'assets/images/simf_logo.png',
      width: size,
      height: size,
    );
  }
}
