import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/media_partners/data/media_partner_models.dart';
import 'package:simf_app/features/media_partners/data/media_partners_endpoints.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// `GET /app/media-partners` → the flat partner list (public, D-199).
final mediaPartnersProvider =
    FutureProvider.autoDispose<List<MediaPartner>>((ref) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<List<MediaPartner>>(
    MediaPartnersEndpoints.list,
    decodeData: (data) =>
        ((data is Map ? data['items'] : null) as List? ?? const <dynamic>[])
            .whereType<Map<dynamic, dynamic>>()
            .map((e) => MediaPartner.fromJson(e.cast<String, dynamic>()))
            .toList(growable: false),
  );
});
