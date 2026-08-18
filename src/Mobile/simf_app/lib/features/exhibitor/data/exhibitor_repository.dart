import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_endpoints.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_models.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

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
      ExhibitorEndpoints.scanVisitor,
      body: <String, dynamic>{
        'qrId': qrId.trim(),
        if (note != null && note.trim().isNotEmpty) 'note': note.trim(),
      },
      decodeData: VisitorCard.fromData,
    );
  }

  /// The BOOTH's captured visitors, newest first. FR-EXH-003 — the list is
  /// scoped to the caller's exhibitor, not to the caller: every officer of the
  /// booth sees the same leads.
  Future<List<ExhibitorVisitor>> listMyVisitors() {
    return _client.get<List<ExhibitorVisitor>>(
      ExhibitorEndpoints.visitors,
      decodeData: ExhibitorVisitor.listFromData,
    );
  }

  /// `DELETE /app/exhibitor/visitors/{id}` — FR-EXH-002: drop one captured lead
  /// from the booth's list (soft-delete server-side, idempotent).
  Future<void> removeVisitor(String id) {
    return _client.delete<bool>(
      ExhibitorEndpoints.visitorById(id),
      decodeData: (_) => true,
    );
  }

  /// `GET /app/exhibitor/visitors/{id}/vcard` — FR-EXH-002: raw vCard 3.0 text
  /// for one captured lead, the same export My Contacts offers.
  Future<String> getVcard(String id) =>
      _client.getText(ExhibitorEndpoints.visitorVcard(id));
}

final exhibitorRepositoryProvider = Provider<ExhibitorRepository>((ref) {
  return ExhibitorRepository(ref.watch(simfApiClientProvider));
});

/// The booth's captured leads (`GET /app/exhibitor/visitors`).
///
/// A THIRD shape, after the fold-to-null of `termsBlockProvider` and the plain
/// list of `savedContactsProvider`: the 403 stays an ERROR and the screen
/// branches on it inside `when`'s error callback. A 403 here means "your
/// account is not linked to a booth yet", which is a failure with its own copy
/// — not an empty result — so folding it into the data branch would be lying
/// about what happened.
final myVisitorsProvider = FutureProvider.autoDispose<List<ExhibitorVisitor>>(
  (ref) => ref.watch(exhibitorRepositoryProvider).listMyVisitors(),
);
