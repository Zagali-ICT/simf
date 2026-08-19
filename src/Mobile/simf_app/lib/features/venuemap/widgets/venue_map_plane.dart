import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/venuemap/data/venue_map_data.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';
import 'package:simf_app/features/venuemap/widgets/venue_map_marker.dart';

/// The pan/zoom node plane — the venue 2D map itself, one marker per node on
/// the fixed [venueMapCanvas] design-space square.
class VenueMapPlane extends StatelessWidget {
  const VenueMapPlane({
    required this.transform,
    required this.nodes,
    required this.positions,
    required this.selectedId,
    required this.isArabic,
    required this.onSelect,
    super.key,
  });

  final TransformationController transform;
  final List<VenueMapNode> nodes;
  final Map<String, Offset> positions;
  final String? selectedId;
  final bool isArabic;
  final ValueChanged<VenueMapNode> onSelect;

  @override
  Widget build(BuildContext context) {
    // The canvas geometry must NOT mirror in RTL (node positions are
    // physical venue coordinates, L-3) — force LTR for the map plane.
    return Directionality(
      textDirection: TextDirection.ltr,
      child: InteractiveViewer(
        constrained: false,
        transformationController: transform,
        minScale: 0.3,
        maxScale: 4,
        boundaryMargin: const EdgeInsets.all(SimfTokens.venueMapPanMargin),
        child: SizedBox(
          width: venueMapCanvas,
          height: venueMapCanvas,
          child: Stack(
            children: <Widget>[
              for (final node in nodes)
                Positioned(
                  left: (positions[node.id] ?? Offset.zero).dx - 40,
                  top: (positions[node.id] ?? Offset.zero).dy - 40,
                  width: SimfTokens.venueMapScreenWidth,
                  child: VenueMapMarker(
                    node: node,
                    isArabic: isArabic,
                    selected: node.id == selectedId,
                    onTap: () => onSelect(node),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
