import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'contact_models.dart';

/// Data layer for visitor-to-visitor contact sharing (SIMF-FDS-014 §5.7, D-286).
/// Every call requires an **Approved** account (`RequireApprovedAccount` + the
/// app token); there is no permission code (self-service, like Connection). All
/// methods throw [ApiFailure] on a wire error — the screens map 404 (unknown /
/// revoked token) and 400 (save yourself) to dedicated messages.
class ContactsRepository {
  ContactsRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/account/share-token` → the caller's token, minted on first call.
  Future<String> getMyShareToken() {
    return _client
        .get<VisitorShareToken>(
          '/app/account/share-token',
          decodeData: VisitorShareToken.fromData,
        )
        .then((t) => t.token);
  }

  /// `POST /app/account/share-token/rotate` → a fresh token (the old one stops
  /// resolving). Rate-limited server-side under the `auth` bucket.
  Future<String> rotateShareToken() {
    return _client
        .post<VisitorShareToken>(
          '/app/account/share-token/rotate',
          decodeData: VisitorShareToken.fromData,
        )
        .then((t) => t.token);
  }

  /// `POST /app/contacts/resolve` { token } → the owner's live card. A 404 means
  /// the token is unknown or was rotated away (E2E-MMC-008).
  Future<VisitorCard> resolve(String token) {
    return _client.post<VisitorCard>(
      '/app/contacts/resolve',
      body: <String, dynamic>{'token': token},
      decodeData: VisitorCard.fromData,
    );
  }

  /// `POST /app/contacts/save` { token, note? } → the saved row. Idempotent per
  /// (owner, subject); saving your own token is a 400 (E2E-MMC-009).
  Future<SavedContactRow> save(String token, String? note) {
    return _client.post<SavedContactRow>(
      '/app/contacts/save',
      body: <String, dynamic>{
        'token': token,
        if (note != null && note.trim().isNotEmpty) 'note': note.trim(),
      },
      decodeData: SavedContactRow.fromData,
    );
  }

  /// `GET /app/contacts` → the caller's My-Contacts list (resolved on read).
  Future<List<SavedContactRow>> listSaved() {
    return _client.get<List<SavedContactRow>>(
      '/app/contacts',
      decodeData: SavedContactRow.listFromData,
    );
  }

  /// `DELETE /app/contacts/{id}` → soft-deletes one saved contact (idempotent).
  Future<void> remove(String id) {
    return _client.delete<bool>(
      '/app/contacts/$id',
      decodeData: (_) => true,
    );
  }

  /// `GET /app/contacts/{id}/vcard` → raw vCard 3.0 text for a saved contact.
  Future<String> getVcard(String id) => _client.getText('/app/contacts/$id/vcard');
}

final contactsRepositoryProvider = Provider<ContactsRepository>((ref) {
  return ContactsRepository(ref.watch(simfApiClientProvider));
});
