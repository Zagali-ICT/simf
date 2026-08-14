import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';
import 'package:simf_app/features/venuemap/data/venuemap_endpoints.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// App-local data layer for the 2D venue map (Page_015). All three reads are
/// **public** (`AllowAnonymous`) — a Guest sees the full map. Throws
/// [ApiFailure] on a wire error; a 404 on the detail means the booth is
/// unknown/inactive (the popup keeps the summary — Page_015 L-8).
class VenueMapRepository {
  VenueMapRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/venue-map` → the full active node list (E1).
  Future<List<VenueMapNode>> getNodes() {
    return _client.get<List<VenueMapNode>>(
      VenueMapEndpoints.venueMap,
      decodeData: (data) =>
          _list(data).map(VenueMapNode.fromJson).toList(growable: false),
    );
  }

  /// `GET /app/booths` → the booth summaries that fill the popups (E2).
  Future<List<BoothSummary>> getBooths() {
    return _client.get<List<BoothSummary>>(
      VenueMapEndpoints.booths,
      decodeData: (data) =>
          _list(data).map(BoothSummary.fromJson).toList(growable: false),
    );
  }

  /// `GET /app/booths/{id}` → the booth detail (description paragraph, E3).
  Future<BoothDetail> getBoothDetail(String id) {
    return _client.get<BoothDetail>(
      VenueMapEndpoints.boothById(id),
      decodeData: (data) => BoothDetail.fromJson(_asMap(data)),
    );
  }

  static List<Map<String, dynamic>> _list(Object? data) =>
      (data as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => e.cast<String, dynamic>())
          .toList(growable: false);

  static Map<String, dynamic> _asMap(Object? data) =>
      (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
}

final venueMapRepositoryProvider = Provider<VenueMapRepository>((ref) {
  return VenueMapRepository(ref.watch(simfApiClientProvider));
});
