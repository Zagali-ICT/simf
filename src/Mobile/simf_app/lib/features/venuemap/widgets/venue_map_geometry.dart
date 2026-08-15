import 'package:simf_app/features/venuemap/data/venue_map_models.dart';

/// The min/max extent of the loaded nodes, used to normalise `(x, y)` into
/// `[0, 1]` before mapping onto the canvas (Page_015 L-4).
class VenueMapBounds {
  const VenueMapBounds(this.minX, this.maxX, this.minY, this.maxY);

  factory VenueMapBounds.of(List<VenueMapNode> nodes) {
    var minX = nodes.first.x;
    var maxX = nodes.first.x;
    var minY = nodes.first.y;
    var maxY = nodes.first.y;
    for (final node in nodes) {
      minX = node.x < minX ? node.x : minX;
      maxX = node.x > maxX ? node.x : maxX;
      minY = node.y < minY ? node.y : minY;
      maxY = node.y > maxY ? node.y : maxY;
    }
    return VenueMapBounds(minX, maxX, minY, maxY);
  }

  final double minX;
  final double maxX;
  final double minY;
  final double maxY;

  double normX(double x) =>
      (maxX - minX) <= 0 ? 0.5 : (x - minX) / (maxX - minX);
  double normY(double y) =>
      (maxY - minY) <= 0 ? 0.5 : (y - minY) / (maxY - minY);
}
