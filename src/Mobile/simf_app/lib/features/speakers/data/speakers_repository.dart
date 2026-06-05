import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import 'speaker_models.dart';

/// Data layer for the speakers list (Page_019) + profile (Page_020). The two
/// reads are **public** (`AllowAnonymous`, D-199); the meeting request is
/// **approved-only** (D-269). Throws [ApiFailure] on a wire error — the screens
/// map 404 (`SPEAKER_NOT_FOUND`) and 409 (`SPEAKER_MEETING_REQUESTS_NOT_ALLOWED`).
class SpeakersRepository {
  SpeakersRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/speakers` → the ordered speaker summaries (E1).
  Future<List<SpeakerSummary>> getSpeakers() {
    return _client.get<List<SpeakerSummary>>(
      '/app/speakers',
      decodeData: (data) =>
          ((data is Map ? data['items'] : null) as List? ?? const <dynamic>[])
              .whereType<Map<dynamic, dynamic>>()
              .map((e) => SpeakerSummary.fromJson(e.cast<String, dynamic>()))
              .toList(growable: false),
    );
  }

  /// `GET /app/speakers/{id}` → the full profile (E1). 404 when missing.
  Future<SpeakerDetail> getSpeaker(String id) {
    return _client.get<SpeakerDetail>(
      '/app/speakers/$id',
      decodeData: (data) => SpeakerDetail.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  }

  /// `POST /app/speakers/{id}/meeting-requests` (approved-only, E2). The body is
  /// `{ requesterName, subject }`; the response is discarded (success is enough).
  Future<void> submitMeetingRequest(
    String speakerId, {
    required String requesterName,
    required String subject,
  }) {
    return _client.post<bool>(
      '/app/speakers/$speakerId/meeting-requests',
      body: <String, dynamic>{
        'requesterName': requesterName,
        'subject': subject,
      },
      decodeData: (_) => true,
    );
  }
}

final speakersRepositoryProvider = Provider<SpeakersRepository>((ref) {
  return SpeakersRepository(ref.watch(simfApiClientProvider));
});
