import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Data layer for sending a question (Page_026). One write, reusing the shipped
/// endpoint (no new API — D-169/D-174): `POST /app/sessions/{id}/questions`
/// (`RequireApprovedAccount`). S-5 — the `isAtVenue` flag is sent `false`: the
/// app does not self-certify presence. The server is the authoritative LIVE gate
/// (hall arrival via geofence [D-242] or a hall-door gate scan); where the hall
/// has no arrival mechanism the server accepts the question so remote Q&A works.
///
/// Throws [ApiFailure] on a wire error — the screen maps a 400
/// (`SESSION_NOT_LIVE_FOR_QUESTIONS`) to the "questions not open" toast, a 404
/// to the same not-open toast, and any other failure to a generic error toast.
class QuestionsRepository {
  QuestionsRepository(this._client);

  final SimfApiClient _client;

  /// `POST /app/sessions/{sessionId}/questions` → submits the question to the
  /// chosen recipient. [recipientIndex] is the wire int (Speaker=0, Host=1).
  Future<void> submitQuestion(
    String sessionId, {
    required String questionText,
    required int recipientIndex,
  }) {
    return _client.post<bool>(
      '/app/sessions/$sessionId/questions',
      body: <String, dynamic>{
        'questionText': questionText,
        // S-5 (owner) — the app does NOT self-certify venue presence: hardcoding
        // `true` let any remote user post live questions. The real LIVE gate is
        // server-side hall arrival (a HallAttendance record from the geofence or
        // a hall-door gate scan); where the hall has no arrival mechanism the
        // server accepts the question (remote Q&A still works). The flag stays on
        // the request for wire-compat (D-219) but is no longer trusted as a gate.
        'isAtVenue': false,
        'recipient': recipientIndex,
      },
      decodeData: (_) => true,
    );
  }
}

final questionsRepositoryProvider = Provider<QuestionsRepository>((ref) {
  return QuestionsRepository(ref.watch(simfApiClientProvider));
});
