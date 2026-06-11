import 'package:flutter/material.dart';

/// The SIMF palm-and-anchor brand mark (KSA-Project Figma node 159:580),
/// bundled once at 4x (544 px) so it renders crisp from the 44 px sign-in
/// header up to the 136 px splash hero (D-359).
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
