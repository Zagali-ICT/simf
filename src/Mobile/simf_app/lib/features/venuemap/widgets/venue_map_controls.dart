import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// One floating gold map control (frame nodes: locate / + / −).
class VenueMapControl extends StatelessWidget {
  const VenueMapControl({required this.icon, required this.onTap, super.key});

  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // Frame 758:1358 — gold square controls, 4-px radius, 20-px navy glyph.
    return Material(
      color: SimfTokens.accent,
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: SizedBox(
          width: SimfTokens.mapControlSize,
          height: SimfTokens.mapControlSize,
          child: Icon(icon, size: 20, color: SimfTokens.navy),
        ),
      ),
    );
  }
}
