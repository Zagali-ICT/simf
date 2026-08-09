import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/gallery/data/gallery_endpoints.dart';
import 'package:simf_app/features/gallery/data/media_models.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// `GET /app/media` → the media items (public, D-199).
final mediaItemsProvider =
    FutureProvider.autoDispose<List<MediaItem>>((ref) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<List<MediaItem>>(
    GalleryEndpoints.media,
    decodeData: (data) =>
        ((data is Map ? data['items'] : null) as List? ?? const <dynamic>[])
            .whereType<Map<dynamic, dynamic>>()
            .map((e) => MediaItem.fromJson(e.cast<String, dynamic>()))
            .toList(growable: false),
  );
});
