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

  /// `GET /app/speakers/{id}/available-slots` (approved-only, D-474) → the free
  /// meeting slots derived from the speaker's availability windows.
  Future<List<SpeakerSlot>> getAvailableSlots(String speakerId) {
    return _client.get<List<SpeakerSlot>>(
      '/app/speakers/$speakerId/available-slots',
      decodeData: (data) => (data as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => SpeakerSlot.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false),
    );
  }

  /// `POST /app/speakers/{id}/meeting-requests` (approved-only, E2). The body is
  /// `{ requesterName, subject }` plus an optional picked slot (D-474/D-475 — the
  /// VIP slot flow; the server requires VIP + a free slot when one is sent). The
  /// response is discarded (success is enough).
  Future<void> submitMeetingRequest(
    String speakerId, {
    required String requesterName,
    required String subject,
    DateTime? slotStart,
    DateTime? slotEnd,
  }) {
    return _client.post<bool>(
      '/app/speakers/$speakerId/meeting-requests',
      body: <String, dynamic>{
        'requesterName': requesterName,
        'subject': subject,
        if (slotStart != null)
          'slotStart': slotStart.toUtc().toIso8601String(),
        if (slotEnd != null)
          'slotEnd': slotEnd.toUtc().toIso8601String(),
      },
      decodeData: (_) => true,
    );
  }
}

/// D-474 (#11) — one bookable meeting slot offered by a speaker.
class SpeakerSlot {
  const SpeakerSlot({required this.start, required this.end});

  factory SpeakerSlot.fromJson(Map<String, dynamic> json) => SpeakerSlot(
        start: DateTime.parse(json['start'] as String).toUtc(),
        end: DateTime.parse(json['end'] as String).toUtc(),
      );

  final DateTime start;
  final DateTime end;
}

final speakersRepositoryProvider = Provider<SpeakersRepository>((ref) {
  return SpeakersRepository(ref.watch(simfApiClientProvider));
});
