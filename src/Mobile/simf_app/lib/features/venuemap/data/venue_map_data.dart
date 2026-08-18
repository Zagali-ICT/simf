import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/venuemap/data/venue_map_geometry.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';
import 'package:simf_app/features/venuemap/data/venue_map_repository.dart';

/// The design-space canvas the normalised node coordinates map onto. Moved out
/// of the State so the provider can derive positions without one.
const double venueMapCanvas = 1000;
const double _venueMapPad = 80;

/// Normalises every node's `(x, y)` onto the canvas once (Page_015 L-4).
Map<String, Offset> _canvasPositions(List<VenueMapNode> nodes) {
  if (nodes.isEmpty) {
    return const <String, Offset>{};
  }
  const span = venueMapCanvas - 2 * _venueMapPad;
  final bounds = VenueMapBounds.of(nodes);
  return <String, Offset>{
    for (final node in nodes)
      node.id: Offset(
        _venueMapPad + bounds.normX(node.x) * span,
        _venueMapPad + bounds.normY(node.y) * span,
      ),
  };
}

/// The map's nodes, plus the two lookups derived from them ONCE.
///
/// The derivation used to happen in `setState` after the load, which is what
/// made those two maps state. Computing them here keeps them beside the data
/// they come from, and there is exactly one place that can get them out of step
/// with it: none.
@immutable
class VenueMapData {
  const VenueMapData({
    required this.nodes,
    required this.positions,
    required this.boothById,
  });

  final List<VenueMapNode> nodes;
  final Map<String, Offset> positions;
  final Map<String, BoothSummary> boothById;

  BoothSummary? boothFor(VenueMapNode node) =>
      node.boothId == null ? null : boothById[node.boothId];
}

/// The venue map (`GET /app/venue-map/nodes` + `GET /app/booths`).
final venueMapDataProvider = FutureProvider.autoDispose<VenueMapData>(
  (ref) async {
    final repo = ref.watch(venueMapRepositoryProvider);
    // Both reads in flight together (L-1); the screen is ready when both land.
    final results = await Future.wait(<Future<Object>>[
      repo.getNodes(),
      repo.getBooths(),
    ]);
    final nodes = results[0] as List<VenueMapNode>;
    final booths = results[1] as List<BoothSummary>;
    return VenueMapData(
      nodes: nodes,
      positions: _canvasPositions(nodes),
      boothById: <String, BoothSummary>{
        for (final booth in booths) booth.id: booth,
      },
    );
  },
);
