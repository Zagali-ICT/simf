import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'banner_models.dart';

/// `GET /app/banners` → the active, time-windowed, display-ordered home banners
/// (public, D-173). Feeds the rotating home hero (#43); best-effort — the home
/// falls back to the static hero when the list is empty or on error.
final bannersProvider =
    FutureProvider.autoDispose<List<PublicBannerItem>>((ref) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<List<PublicBannerItem>>(
    '/app/banners',
    decodeData: PublicBannerItem.listFromData,
  );
});
