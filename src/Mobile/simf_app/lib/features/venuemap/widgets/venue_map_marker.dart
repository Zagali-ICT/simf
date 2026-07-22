import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../data/venue_map_models.dart';

/// One node marker, styled by [VenueMapNode.kind]; the selected node carries
/// a gold ring. All markers are tappable (selection drives the info card).
class VenueMapMarker extends StatelessWidget {
  const VenueMapMarker({
    required this.node,
    required this.isArabic,
    required this.selected,
    required this.onTap,
    super.key,
  });

  final VenueMapNode node;
  final bool isArabic;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final style = _markerStyle(node.kind);
    final marker = Column(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Container(
          width: 34,
          height: 34,
          decoration: BoxDecoration(
            color: style.fill,
            shape: style.shape,
            borderRadius: style.shape == BoxShape.rectangle
                ? BorderRadius.circular(SimfTokens.radiusSmall)
                : null,
            border: Border.all(
              color: selected ? SimfTokens.accent : style.border,
              width: selected ? 3 : 1.5,
            ),
          ),
          child: Icon(style.icon, size: 18, color: style.foreground),
        ),
        const SizedBox(height: SimfTokens.gap2),
        Text(
          node.localizedLabel(isArabic),
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          textAlign: TextAlign.center,
          style: SimfTokens.labelWhiteSemibold9,
        ),
      ],
    );

    return GestureDetector(
      onTap: onTap,
      behavior: HitTestBehavior.opaque,
      child: Semantics(
        button: true,
        label: node.localizedLabel(isArabic),
        child: marker,
      ),
    );
  }

  _MarkerStyle _markerStyle(VenueMapNodeKind kind) {
    switch (kind) {
      case VenueMapNodeKind.hall:
        return const _MarkerStyle(
          icon: Icons.meeting_room_outlined,
          fill: SimfTokens.navy,
          foreground: SimfTokens.surface,
          border: SimfTokens.beigeBorder,
          shape: BoxShape.rectangle,
        );
      case VenueMapNodeKind.zone:
        return const _MarkerStyle(
          icon: Icons.crop_din,
          fill: SimfTokens.navyDeep,
          foreground: SimfTokens.beigeBorder,
          border: SimfTokens.beigeBorder,
          shape: BoxShape.rectangle,
        );
      case VenueMapNodeKind.booth:
        return const _MarkerStyle(
          icon: Icons.storefront,
          fill: SimfTokens.accent,
          foreground: SimfTokens.navy,
          border: SimfTokens.accent,
          shape: BoxShape.circle,
        );
      case VenueMapNodeKind.pointOfInterest:
        return const _MarkerStyle(
          icon: Icons.place,
          fill: SimfTokens.surface,
          foreground: SimfTokens.danger,
          border: SimfTokens.danger,
          shape: BoxShape.circle,
        );
    }
  }
}

class _MarkerStyle {
  const _MarkerStyle({
    required this.icon,
    required this.fill,
    required this.foreground,
    required this.border,
    required this.shape,
  });

  final IconData icon;
  final Color fill;
  final Color foreground;
  final Color border;
  final BoxShape shape;
}
