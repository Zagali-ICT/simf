import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';

/// One floating gold map control (frame nodes: locate / + / −).
class VenueMapControl extends StatelessWidget {
  const VenueMapControl({
    required this.icon,
    required this.label,
    required this.onTap,
    super.key,
  });

  final IconData icon;

  /// The control's accessible name (zoom in / zoom out / reset the view). The
  /// controls are icon-only, so without it a screen reader announced three
  /// unnamed views and the map could not be zoomed (BUG-012).
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    // Frame 758:1358 — gold square controls, 4-px radius, 20-px navy glyph.
    return Semantics(
      button: true,
      label: label,
      child: Material(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          child: SizedBox(
            width: SimfTokens.mapControlSize,
            height: SimfTokens.mapControlSize,
            child: Icon(icon, size: SimfTokens.venueMapControlsSize, color: SimfTokens.navy),
          ),
        ),
      ),
    );
  }
}
