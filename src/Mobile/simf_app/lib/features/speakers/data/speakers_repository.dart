import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/speakers/data/speaker_models.dart';
import 'package:simf_app/features/speakers/data/speakers_endpoints.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Data layer for the speakers list (Page_019) + profile (Page_020). The two
/// reads are **public** (`AllowAnonymous`, D-199); the meeting request is
/// **approved-only** (D-269). Throws [ApiFailure] on a wire error — the screens
/// map 404 (`SPEAKER_NOT_FOUND`) and 409
/// (`SPEAKER_MEETING_REQUESTS_NOT_ALLOWED`).
class SpeakersRepository {
  SpeakersRepository(this._client);

  final SimfApiClient _client;

  /// `GET /app/speakers` → the ordered speaker summaries (E1).
  Future<List<SpeakerSummary>> getSpeakers() {
    return _client.get<List<SpeakerSummary>>(
      SpeakersEndpoints.list,
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
      SpeakersEndpoints.byId(id),
      decodeData: (data) => SpeakerDetail.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  }

  /// `GET /app/speakers/{id}/available-slots` (approved-only, D-474) → the free
  /// meeting slots derived from the speaker's availability windows.
  Future<List<SpeakerSlot>> getAvailableSlots(String speakerId) {
    return _client.get<List<SpeakerSlot>>(
      SpeakersEndpoints.availableSlots(speakerId),
      decodeData: (data) => (data as List? ?? const <dynamic>[])
          .whereType<Map<dynamic, dynamic>>()
          .map((e) => SpeakerSlot.fromJson(e.cast<String, dynamic>()))
          .toList(growable: false),
    );
  }

  /// `POST /app/speakers/{id}/meeting-requests` (approved-only, E2). The body
  /// is `{ requesterName, subject }` plus an optional picked slot (D-474/D-475
  /// — the VIP slot flow; the server requires VIP + a free slot when one is
  /// sent). The response is discarded (success is enough).
  Future<void> submitMeetingRequest(
    String speakerId, {
    required String requesterName,
    required String subject,
    DateTime? slotStart,
    DateTime? slotEnd,
  }) {
    return _client.post<bool>(
      SpeakersEndpoints.meetingRequests(speakerId),
      body: <String, dynamic>{
        'requesterName': requesterName,
        'subject': subject,
        if (slotStart != null)
          'slotStart': formatWire(slotStart),
        if (slotEnd != null)
          'slotEnd': formatWire(slotEnd),
      },
      decodeData: (_) => true,
    );
  }
}

final speakersRepositoryProvider = Provider<SpeakersRepository>((ref) {
  return SpeakersRepository(ref.watch(simfApiClientProvider));
});

/// The speaker directory (`GET /app/speakers`).
///
/// Only the LOAD lives here. `SpeakersScreen` keeps its
/// `ConsumerStatefulWidget` because `_query` and `_alphaSorted` are real UI
/// state that belongs to the widget and has no server behind it — the search
/// box and the A→Z toggle.
final speakersListProvider = FutureProvider.autoDispose<List<SpeakerSummary>>(
  (ref) => ref.watch(speakersRepositoryProvider).getSpeakers(),
);

/// One speaker, or **null when the server has no such id** (a 404).
///
/// The `newsArticleProvider` shape: a 404 is "this speaker is gone", which the
/// screen answers with its own not-found copy rather than the error surface.
final speakerDetailProvider =
    FutureProvider.autoDispose.family<SpeakerDetail?, String>((ref, id) async {
  try {
    return await ref.watch(speakersRepositoryProvider).getSpeaker(id);
  } on ApiFailure catch (failure) {
    if (failure.httpStatus == 404) {
      return null;
    }
    rethrow;
  }
});
