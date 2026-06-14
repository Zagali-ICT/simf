import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../contacts/data/contact_models.dart';
import 'exhibitor_models.dart';

/// D-426 — exhibitor ("Other" profile type) lead capture. Scans a visitor's
/// entry-badge QR (`POST /app/exhibitor/visitors/scan`) → captures them to the
/// exhibitor's "My Visitors" list and returns the full card; lists the captured
/// visitors (`GET /app/exhibitor/visitors`). Both require an Approved account +
/// a non-visitor (exhibitor) profile type — a visitor-tier caller gets 403. All
/// methods throw [ApiFailure] on a wire error; the screens map 404 (no matching
/// badge) / 403 (not an exhibitor) to dedicated messages.
class ExhibitorRepository {
  ExhibitorRepository(this._client);

  final SimfApiClient _client;

  /// Scan a visitor badge by its QR id → the captured visitor's full card.
  Future<VisitorCard> scanByBadge(String qrId, {String? note}) {
    return _client.post<VisitorCard>(
      '/app/exhibitor/visitors/scan',
      body: <String, dynamic>{
        'qrId': qrId.trim(),
        if (note != null && note.trim().isNotEmpty) 'note': note.trim(),
      },
      decodeData: VisitorCard.fromData,
    );
  }

  /// The exhibitor's captured visitors, newest first.
  Future<List<ExhibitorVisitor>> listMyVisitors() {
    return _client.get<List<ExhibitorVisitor>>(
      '/app/exhibitor/visitors',
      decodeData: ExhibitorVisitor.listFromData,
    );
  }
}

final exhibitorRepositoryProvider = Provider<ExhibitorRepository>((ref) {
  return ExhibitorRepository(ref.watch(simfApiClientProvider));
});
