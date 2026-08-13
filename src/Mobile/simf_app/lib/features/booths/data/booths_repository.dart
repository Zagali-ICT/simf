import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/venuemap/data/venue_map_models.dart';
import 'package:simf_app/features/venuemap/data/venue_map_repository.dart';

/// `GET /app/booths/{id}` → the booth/exhibitor detail (Figma 1439:11881).
///
/// The read itself belongs to the venue-map repository, which already owns the
/// booth endpoints; this provider is the booths feature's entry point into it.
final exhibitorDetailProvider =
    FutureProvider.autoDispose.family<BoothDetail, String>((ref, id) async {
  return ref.watch(venueMapRepositoryProvider).getBoothDetail(id);
});
