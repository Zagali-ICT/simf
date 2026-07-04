import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'meet_models.dart';

/// `GET /app/account/recommendations/meet-like-you` → the visitor's "meet
/// someone like you" matches (RequireApprovedAccount).
final meetRecommendationsProvider =
    FutureProvider.autoDispose<List<Recommendation>>((ref) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<List<Recommendation>>(
    '/app/account/recommendations/meet-like-you',
    decodeData: Recommendation.listFromData,
  );
});
