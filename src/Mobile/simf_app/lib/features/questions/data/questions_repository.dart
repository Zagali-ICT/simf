import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Data layer for sending a question (Page_026). One write, reusing the shipped
/// endpoint (no new API — D-169/D-174): `POST /app/sessions/{id}/questions`
/// (`RequireApprovedAccount`). The `isAtVenue` self-assert flag is sent `true`
/// (the app's submit screen is reached from a live session — D-171); the server
/// is the authoritative venue gate when the hall has a geofence (D-242).
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
        'isAtVenue': true,
        'recipient': recipientIndex,
      },
      decodeData: (_) => true,
    );
  }
}

final questionsRepositoryProvider = Provider<QuestionsRepository>((ref) {
  return QuestionsRepository(ref.watch(simfApiClientProvider));
});
