import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/sponsors/data/sponsor_models.dart';
import 'package:simf_app/features/sponsors/data/sponsors_endpoints.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// `GET /app/sponsors` → the tier-grouped sponsors (public, D-199).
final sponsorGroupsProvider =
    FutureProvider.autoDispose<List<SponsorTierGroup>>((ref) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<List<SponsorTierGroup>>(
    SponsorsEndpoints.list,
    decodeData: SponsorTierGroup.listFromData,
  );
});

/// `GET /app/sponsors/{id}` → the full sponsor detail (Figma 1439:11826).
final sponsorDetailProvider =
    FutureProvider.autoDispose.family<SponsorDetail, String>((ref, id) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<SponsorDetail>(
    SponsorsEndpoints.byId(id),
    decodeData: SponsorDetail.fromData,
  );
});
