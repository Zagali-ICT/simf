import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'delegation_models.dart';

/// Data layer for the Delegations screen — App "الوفود" (Figma 1426:10771). One
/// read of the public invited-country delegations (`GET /app/delegations`,
/// anonymous). Throws [ApiFailure] on a wire error — the screen maps it to the
/// error + retry surface.
class DelegationsRepository {
  DelegationsRepository(this._client);

  final SimfApiClient _client;

  Future<Delegations> getDelegations() {
    return _client.get<Delegations>(
      '/app/delegations',
      decodeData: Delegations.fromData,
    );
  }
}

final delegationsRepositoryProvider = Provider<DelegationsRepository>((ref) {
  return DelegationsRepository(ref.watch(simfApiClientProvider));
});

/// The invited-country delegations (auto-disposed so it re-reads each visit).
final delegationsProvider =
    FutureProvider.autoDispose<Delegations>((ref) async {
  return ref.watch(delegationsRepositoryProvider).getDelegations();
});
