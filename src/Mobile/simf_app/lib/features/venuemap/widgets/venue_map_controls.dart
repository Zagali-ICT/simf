import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';

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
            child: Icon(icon,
                size: SimfTokens.venueMapControlsSize, color: SimfTokens.navy,),
          ),
        ),
      ),
    );
  }
}

/// The floating gold control stack on the map's inline end (frame 758:1358):
/// recentre, zoom in, zoom out.
class VenueMapControlBar extends StatelessWidget {
  const VenueMapControlBar({
    required this.onReset,
    required this.onZoomIn,
    required this.onZoomOut,
    super.key,
  });

  final VoidCallback onReset;
  final VoidCallback onZoomIn;
  final VoidCallback onZoomOut;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Column(
      children: <Widget>[
        VenueMapControl(
          icon: Icons.my_location,
          label: l10n.venueMapResetView,
          onTap: onReset,
        ),
        const SizedBox(height: SimfTokens.space2),
        VenueMapControl(
          icon: Icons.add,
          label: l10n.venueMapZoomIn,
          onTap: onZoomIn,
        ),
        const SizedBox(height: SimfTokens.space2),
        VenueMapControl(
          icon: Icons.remove,
          label: l10n.venueMapZoomOut,
          onTap: onZoomOut,
        ),
      ],
    );
  }
}
