import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/archive/data/archive_endpoints.dart';
import 'package:simf_app/features/archive/data/archive_models.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// `GET /app/archive` → the past editions (public, D-273).
final archiveEditionsProvider =
    FutureProvider.autoDispose<List<ArchiveEdition>>((ref) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<List<ArchiveEdition>>(
    ArchiveEndpoints.list,
    decodeData: ArchiveEdition.listFromData,
  );
});

/// `GET /app/archive/{id}` → the fuller detail (location + date label) for one
/// edition, lazily loaded when an edition pill is selected (D-273).
final archiveEditionDetailProvider = FutureProvider.autoDispose
    .family<ArchiveEditionDetail?, String>((ref, id) async {
  final client = ref.watch(simfApiClientProvider);
  try {
    return await client.get<ArchiveEditionDetail>(
      ArchiveEndpoints.byId(id),
      decodeData: (data) => ArchiveEditionDetail.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  } on ApiFailure {
    return null;
  }
});
