import 'dart:typed_data';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'presentation_models.dart';
import 'sessions_endpoints.dart';

/// Data layer for the session-presentations screen — App "الجلسات"
/// (Figma 1388:7621). Lists every active session (`GET /app/presentations`;
/// D-704 — not only the sessions that have an uploaded deck) and fetches one
/// file's bytes through the shared client (`GET /app/presentations/{id}/file`)
/// so the self-signed-TLS handling (and the bearer, when signed in) is inherited
/// (a bare URL open could not). Both are **ANONYMOUS** (owner 2026-07-22 — the
/// Sessions list is public, opened from the home "Sessions" tile by a guest); the
/// client simply omits the bearer when there is no session. Throws [ApiFailure]
/// on a wire error.
class PresentationRepository {
  PresentationRepository(this._client);

  final SimfApiClient _client;

  Future<PresentationsPage> list() {
    return _client.get<PresentationsPage>(
      SessionsEndpoints.presentations,
      decodeData: PresentationsPage.fromData,
    );
  }

  /// The stored file bytes for one presentation (for the تحميل share/save). A
  /// 404 surfaces as an [ApiFailure] the screen reports.
  Future<Uint8List> downloadFile(String presentationId) {
    return _client.getBytes(SessionsEndpoints.presentationFile(presentationId));
  }
}

final presentationRepositoryProvider = Provider<PresentationRepository>((ref) {
  return PresentationRepository(ref.watch(simfApiClientProvider));
});

/// The public presentations list (auto-disposed so it re-reads each visit).
final presentationsProvider =
    FutureProvider.autoDispose<PresentationsPage>((ref) async {
  return ref.watch(presentationRepositoryProvider).list();
});
