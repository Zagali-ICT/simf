import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'partner_directory_models.dart';

/// Build #13 — `GET /app/networking/partner-directory` → the "Meet People Like
/// You" partner directory (RequireApprovedAccount): the deduped union of curated
/// Sponsors, Speakers and Booth companies plus the opted-in "Other"-type members.
/// Empty when the CP switch is off.
final partnerDirectoryProvider =
    FutureProvider.autoDispose<List<PartnerDirectoryEntry>>((ref) async {
  final client = ref.watch(simfApiClientProvider);
  return client.get<List<PartnerDirectoryEntry>>(
    '/app/networking/partner-directory',
    decodeData: PartnerDirectoryEntry.listFromData,
  );
});
