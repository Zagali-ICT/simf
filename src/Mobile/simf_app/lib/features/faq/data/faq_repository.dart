import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/faq/data/faq_endpoints.dart';
import 'package:simf_app/features/faq/data/faq_models.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Data layer for the public FAQ accordion (`GET /app/faq`, anonymous —
/// Figma 1388:7567). Returns the active groups (each with its active entries,
/// ordered server-side). Throws [ApiFailure] on a wire error.
class FaqRepository {
  FaqRepository(this._client);

  final SimfApiClient _client;

  Future<List<FaqGroup>> getFaq() {
    return _client.get<List<FaqGroup>>(
      FaqEndpoints.list,
      decodeData: (data) => ((data as List?) ?? const <dynamic>[])
          .map((e) => FaqGroup.fromJson((e as Map).cast<String, dynamic>()))
          .toList(),
    );
  }
}

final faqRepositoryProvider = Provider<FaqRepository>((ref) {
  return FaqRepository(ref.watch(simfApiClientProvider));
});

/// The FAQ list for the screen. Auto-disposes so it refetches each visit.
final faqProvider = FutureProvider.autoDispose<List<FaqGroup>>((ref) {
  return ref.watch(faqRepositoryProvider).getFaq();
});
